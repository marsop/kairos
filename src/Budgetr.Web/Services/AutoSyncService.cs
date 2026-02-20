using Budgetr.Shared.Services;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Budgetr.Web.Services;

/// <summary>
/// Auto-sync service implementation using Rx.NET for change detection and debouncing.
/// Automatically backs up data to the active sync provider when changes are detected.
/// Supports multiple providers (Google Drive, Supabase, etc.) via ISyncProvider.
/// </summary>
public class AutoSyncService : IAutoSyncService
{
    private readonly ITimeTrackingService _timeService;
    private readonly IStorageService _storage;
    private readonly IServiceProvider _serviceProvider;

    private const string EnabledProviderStorageKey = "budgetr_autosync_provider";
    private const string LastSyncStorageKeyPrefix = "budgetr_autosync_lastsync_";
    private const int DebounceMilliseconds = 1000;
    private const int PollingIntervalSeconds = 30;

    private readonly Subject<bool> _changeSubject = new();
    private IDisposable? _subscription;
    private IDisposable? _pollingSubscription;
    private bool _isEnabled;
    private bool _isRestoring;
    private DateTimeOffset? _lastSyncTime;
    private DateTimeOffset? _lastKnownRemoteModifiedTime;
    private AutoSyncStatus _status = AutoSyncStatus.Idle;

    private ISyncProvider? _activeProvider;
    private string? _activeProviderName;

    public bool IsEnabled => _isEnabled;
    public string? ActiveProviderName => _activeProviderName;
    public DateTimeOffset? LastSyncTime => _lastSyncTime;
    public AutoSyncStatus Status => _status;

    public event Action<AutoSyncStatus>? OnStatusChanged;

    public AutoSyncService(
        ITimeTrackingService timeService,
        IStorageService storage,
        IServiceProvider serviceProvider)
    {
        _timeService = timeService;
        _storage = storage;
        _serviceProvider = serviceProvider;

        // Subscribe to time service state changes and push to reactive stream
        _timeService.OnStateChanged += OnDataChanged;
    }

    private ISyncProvider? ResolveProvider(string providerName)
    {
        var providers = _serviceProvider.GetServices<ISyncProvider>();
        return providers?.FirstOrDefault(p => p.Name == providerName);
    }

    private void OnDataChanged()
    {
        // Don't trigger sync if we are currently restoring data from remote
        if (_isEnabled && !_isRestoring)
        {
            _changeSubject.OnNext(true);
        }
    }

    public async Task EnableAsync(string providerName)
    {
        // If already enabled on a different provider, disable first
        if (_isEnabled && _activeProviderName != providerName)
        {
            await DisableAsync();
        }

        if (_isEnabled && _activeProviderName == providerName)
            return;

        // Resolve the provider
        var provider = ResolveProvider(providerName);
        if (provider == null)
        {
            throw new InvalidOperationException($"Sync provider '{providerName}' not found.");
        }

        // Check if the provider is authenticated
        var isSignedIn = await provider.IsAuthenticatedAsync();
        if (!isSignedIn)
        {
            throw new InvalidOperationException($"Please sign in to {providerName} first.");
        }

        _activeProvider = provider;
        _activeProviderName = providerName;
        _isEnabled = true;
        await _storage.SetItemAsync(EnabledProviderStorageKey, providerName);

        // Load last sync time for this provider
        await LoadLastSyncTimeAsync();

        // Initialize last known remote time to avoid immediate restore loop if we just synced
        if (_lastSyncTime.HasValue)
        {
            _lastKnownRemoteModifiedTime = _lastSyncTime;
        }
        else
        {
            // If we have never synced, try to get the current remote time so we only restore *new* changes
            _lastKnownRemoteModifiedTime = await _activeProvider.GetLastBackupTimeAsync();
        }

        // Set up debounced subscription using Rx.NET
        _subscription = _changeSubject
            .Throttle(TimeSpan.FromMilliseconds(DebounceMilliseconds))
            .Subscribe(async _ => await PerformSyncAsync());

        // Set up polling for remote changes
        _pollingSubscription = Observable.Interval(TimeSpan.FromSeconds(PollingIntervalSeconds))
            .Subscribe(async _ => await CheckForRemoteChangesAsync());

        UpdateStatus(AutoSyncStatus.Idle);
    }

    public async Task DisableAsync()
    {
        if (!_isEnabled)
            return;

        _isEnabled = false;
        _activeProvider = null;
        _activeProviderName = null;
        await _storage.SetItemAsync(EnabledProviderStorageKey, "");

        // Dispose subscriptions
        _subscription?.Dispose();
        _subscription = null;

        _pollingSubscription?.Dispose();
        _pollingSubscription = null;

        UpdateStatus(AutoSyncStatus.Idle);
    }

    private async Task PerformSyncAsync()
    {
        if (!_isEnabled || _isRestoring || _activeProvider == null)
            return;

        try
        {
            UpdateStatus(AutoSyncStatus.Syncing);

            // Check if still signed in
            var isSignedIn = await _activeProvider.IsAuthenticatedAsync();
            if (!isSignedIn)
            {
                Console.WriteLine($"Auto-sync: Not signed in to {_activeProviderName}, disabling auto-sync.");
                await DisableAsync();
                UpdateStatus(AutoSyncStatus.Failed);
                return;
            }

            // Export and upload data
            var json = _timeService.ExportData();
            await _activeProvider.UploadDataAsync(json);

            // Update last sync time
            _lastSyncTime = DateTimeOffset.UtcNow;
            var modifiedTime = await _activeProvider.GetLastBackupTimeAsync();
            if (modifiedTime.HasValue)
            {
                _lastKnownRemoteModifiedTime = modifiedTime;
            }

            var storageKey = LastSyncStorageKeyPrefix + _activeProviderName;
            await _storage.SetItemAsync(storageKey, _lastSyncTime.Value.ToString("O"));

            UpdateStatus(AutoSyncStatus.Success);
            Console.WriteLine($"Auto-sync ({_activeProviderName}): Backup completed at {_lastSyncTime}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Auto-sync ({_activeProviderName}) failed: {ex.Message}");
            UpdateStatus(AutoSyncStatus.Failed);
        }
    }

    private async Task CheckForRemoteChangesAsync()
    {
        if (!_isEnabled || _isRestoring || _activeProvider == null)
            return;

        try
        {
            if (!await _activeProvider.IsAuthenticatedAsync())
                return;

            var remoteModified = await _activeProvider.GetLastBackupTimeAsync();

            // If remote file exists and is newer than what we last knew about
            if (remoteModified.HasValue &&
                (!_lastKnownRemoteModifiedTime.HasValue || remoteModified.Value > _lastKnownRemoteModifiedTime.Value + TimeSpan.FromSeconds(1)))
            {
                Console.WriteLine($"Auto-sync ({_activeProviderName}): Detected remote change. Local: {_lastKnownRemoteModifiedTime}, Remote: {remoteModified}");
                await RestoreDataAsync(remoteModified.Value);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Auto-sync ({_activeProviderName}) polling failed: {ex.Message}");
        }
    }

    private async Task RestoreDataAsync(DateTimeOffset remoteModifiedTime)
    {
        if (_activeProvider == null) return;

        try
        {
            _isRestoring = true;
            UpdateStatus(AutoSyncStatus.Syncing);

            var content = await _activeProvider.DownloadDataAsync();
            if (!string.IsNullOrEmpty(content))
            {
                await _timeService.ImportDataAsync(content);
                _lastKnownRemoteModifiedTime = remoteModifiedTime;
                _lastSyncTime = DateTimeOffset.UtcNow;

                var storageKey = LastSyncStorageKeyPrefix + _activeProviderName;
                await _storage.SetItemAsync(storageKey, _lastSyncTime.Value.ToString("O"));

                UpdateStatus(AutoSyncStatus.Success);
                Console.WriteLine($"Auto-sync ({_activeProviderName}): Data restored successfully.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Auto-sync ({_activeProviderName}) restore failed: {ex.Message}");
            UpdateStatus(AutoSyncStatus.Failed);
        }
        finally
        {
            _isRestoring = false;
        }
    }

    private async Task LoadLastSyncTimeAsync()
    {
        try
        {
            var storageKey = LastSyncStorageKeyPrefix + _activeProviderName;
            var lastSyncStr = await _storage.GetItemAsync(storageKey);
            if (!string.IsNullOrEmpty(lastSyncStr) && DateTimeOffset.TryParse(lastSyncStr, out var parsed))
            {
                _lastSyncTime = parsed;
            }
        }
        catch
        {
            // Ignore errors loading last sync time
        }
    }

    private void UpdateStatus(AutoSyncStatus status)
    {
        _status = status;
        OnStatusChanged?.Invoke(status);
    }

    public void Dispose()
    {
        _subscription?.Dispose();
        _pollingSubscription?.Dispose();
        _changeSubject.Dispose();
        _timeService.OnStateChanged -= OnDataChanged;
    }
}
