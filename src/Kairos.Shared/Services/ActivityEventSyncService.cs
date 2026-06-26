using Kairos.Shared.Models;
using Microsoft.Extensions.Logging;

namespace Kairos.Shared.Services;

public sealed class ActivityEventSyncService : IActivityEventSyncService, IDisposable
{
    private readonly ISupabaseActivityEventStore _eventStore;
    private readonly ITimeTrackingService _timeTrackingService;
    private readonly ISupabaseAuthService _authService;
    private readonly ISyncConflictNotifier _conflictNotifier;
    private readonly ISettingsService _settingsService;
    private readonly ISupabaseRealtimeService _realtimeService;
    private readonly ILogger<ActivityEventSyncService> _logger;
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private Timer? _timer;

    // Track the last known state we synced with the server to detect if we have local unpushed changes
    private DateTimeOffset? _lastSyncedLocalModification;

    public ActivityEventSyncService(
        ISupabaseActivityEventStore eventStore,
        ITimeTrackingService timeTrackingService,
        ISupabaseAuthService authService,
        ISyncConflictNotifier conflictNotifier,
        ISettingsService settingsService,
        ISupabaseRealtimeService realtimeService,
        ILogger<ActivityEventSyncService> logger)
    {
        _eventStore = eventStore;
        _timeTrackingService = timeTrackingService;
        _authService = authService;
        _conflictNotifier = conflictNotifier;
        _settingsService = settingsService;
        _realtimeService = realtimeService;
        _logger = logger;

        _realtimeService.OnTableChanged += HandleRemoteTableChanged;
        _realtimeService.OnConnected += HandleRemoteConnected;
    }

    public void StartSync()
    {
        _logger.LogInformation("Starting ActivityEventSyncService background sync timer.");
        _timer = new Timer(OnTimerElapsed, null, TimeSpan.Zero, TimeSpan.FromMinutes(5));
    }

    private void HandleRemoteTableChanged(string table)
    {
        if (table == "activity_events")
        {
            _logger.LogInformation("activity_events table changed via realtime, triggering sync.");
            _ = TriggerImmediateSyncAsync();
        }
    }

    private void HandleRemoteConnected()
    {
        _logger.LogInformation("Supabase Realtime connected, triggering catch-up sync.");
        _ = TriggerImmediateSyncAsync();
    }

    private void OnTimerElapsed(object? state)
    {
        _ = TriggerImmediateSyncAsync();
    }

    public async Task PullFromServerAsync()
    {
        if (!_authService.IsAuthenticated) return;

        await _syncLock.WaitAsync();
        try
        {
            var serverEvents = await _eventStore.LoadEventsAsync();
            _timeTrackingService.UpdateEventsFromServer(serverEvents);
            _lastSyncedLocalModification = _timeTrackingService.Account.LastModifiedAtUtc;
            _settingsService.UpdateLastSupabaseSync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pull events from server.");
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task TriggerImmediateSyncAsync()
    {
        if (!_authService.IsAuthenticated) return;

        // Try to get lock, but don't block forever if a sync is already running
        if (!await _syncLock.WaitAsync(TimeSpan.FromSeconds(1)))
        {
            return;
        }

        try
        {
            var localEvents = _timeTrackingService.Account.Events.ToList();
            var localModification = _timeTrackingService.Account.LastModifiedAtUtc;

            // If localModification is default, it's a fresh install, so we shouldn't consider it a local change
            // unless they actually have local events. Also detect if the local state genuinely diverged from the last sync.
            bool hasLocalChanges = _lastSyncedLocalModification != localModification
                                   && (localModification != default || localEvents.Any());

            var serverEvents = await _eventStore.LoadEventsAsync();

            // For a robust sync, we would check if server data is different.
            // Since we don't have a reliable LastModifiedAtUtc on the server side easily accessible here
            // without pulling the whole list, we pull it and do a hash/count check.
            bool hasServerChanges = DetermineIfServerChanged(localEvents, serverEvents);

            if (hasLocalChanges && hasServerChanges)
            {
                _logger.LogWarning("Conflict detected. Local and server both have changes.");
                // We have a conflict. Ask the user.
                bool useServer = await _conflictNotifier.ResolveConflictAsync();

                if (useServer)
                {
                    _timeTrackingService.UpdateEventsFromServer(serverEvents);
                    _lastSyncedLocalModification = _timeTrackingService.Account.LastModifiedAtUtc;
                }
                else
                {
                    await _eventStore.SaveEventsAsync(localEvents);
                    _lastSyncedLocalModification = localModification;
                }
            }
            else if (hasServerChanges)
            {
                _logger.LogInformation("Server has changes. Overwriting local data.");
                _timeTrackingService.UpdateEventsFromServer(serverEvents);
                _lastSyncedLocalModification = _timeTrackingService.Account.LastModifiedAtUtc;
            }
            else if (hasLocalChanges)
            {
                _logger.LogInformation("Local has changes. Pushing to server.");
                await _eventStore.SaveEventsAsync(localEvents);
                _lastSyncedLocalModification = localModification;
            }

            if (hasServerChanges || hasLocalChanges)
            {
                 _settingsService.UpdateLastSupabaseSync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync activity events.");
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private bool DetermineIfServerChanged(IReadOnlyList<ActivityEvent> local, IReadOnlyList<ActivityEvent> server)
    {
        // Simple heuristic: if count differs, it changed.
        if (local.Count != server.Count) return true;

        // More advanced: check if any event IDs differ or if the end times differ (e.g., stopping an active event)
        var localDict = local.ToDictionary(e => e.Id);
        foreach (var s in server)
        {
            if (!localDict.TryGetValue(s.Id, out var l)) return true; // new event on server
            if (s.EndTime != l.EndTime) return true; // event state changed
            if (s.Comment != l.Comment) return true;
        }

        return false;
    }

    public void Dispose()
    {
        if (_realtimeService is not null)
        {
            _realtimeService.OnTableChanged -= HandleRemoteTableChanged;
            _realtimeService.OnConnected -= HandleRemoteConnected;
        }

        _timer?.Dispose();
        _syncLock.Dispose();
    }
}
