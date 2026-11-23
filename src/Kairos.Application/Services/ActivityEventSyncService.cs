using Kairos.Core.Models;
using Microsoft.Extensions.Logging;

namespace Kairos.Application.Services;

public sealed class ActivityEventSyncService : IActivityEventSyncService, IDisposable
{
    public static readonly TimeSpan SyncInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RealtimeEchoSuppressionWindow = TimeSpan.FromSeconds(2);

    private readonly ISupabaseActivityEventStore _eventStore;
    private readonly ITimeTrackingService _timeTrackingService;
    private readonly ISupabaseAuthService _authService;
    private readonly ISyncConflictNotifier _conflictNotifier;
    private readonly ISettingsService _settingsService;
    private readonly ISupabaseRealtimeService _realtimeService;
    private readonly IPwaService? _pwaService;
    private readonly ILogger<ActivityEventSyncService> _logger;
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private Timer? _timer;
    private bool _isVisible = true;

    // Track the last known event snapshots to detect local/server divergence.
    // Using event content instead of LastModified avoids false positives from unrelated local state changes.
    private IReadOnlyList<ActivityEvent>? _lastSyncedLocalEvents;
    private IReadOnlyList<ActivityEvent>? _lastSyncedServerEvents;
    private DateTimeOffset? _suppressRealtimeUntilUtc;
    private bool _suppressNextLocalStateTriggeredSync;

    public event Action? OnSyncStateChanged;
    public Func<ActivityEventQueryOptions?>? ActiveQueryOptionsProvider { get; set; }

    public ActivityEventSyncService(
        ISupabaseActivityEventStore eventStore,
        ITimeTrackingService timeTrackingService,
        ISupabaseAuthService authService,
        ISyncConflictNotifier conflictNotifier,
        ISettingsService settingsService,
        ISupabaseRealtimeService realtimeService,
        ILogger<ActivityEventSyncService> logger,
        IPwaService? pwaService = null)
    {
        _eventStore = eventStore;
        _timeTrackingService = timeTrackingService;
        _authService = authService;
        _conflictNotifier = conflictNotifier;
        _settingsService = settingsService;
        _realtimeService = realtimeService;
        _logger = logger;
        _pwaService = pwaService;

        _realtimeService.OnTableChanged += HandleRemoteTableChanged;
        _realtimeService.OnConnected += HandleRemoteConnected;
        _timeTrackingService.OnStateChanged += HandleLocalStateChanged;

        if (_pwaService is not null)
        {
            _pwaService.OnVisibilityChanged += HandleVisibilityChanged;
            _isVisible = _pwaService.IsVisible;
        }
    }

    public void StartSync()
    {
        _logger.LogInformation("Starting ActivityEventSyncService background sync timer with {Interval} interval.", SyncInterval);
        var initialDelay = _isVisible ? TimeSpan.Zero : Timeout.InfiniteTimeSpan;
        var interval = _isVisible ? SyncInterval : Timeout.InfiniteTimeSpan;
        _timer = new Timer(OnTimerElapsed, null, initialDelay, interval);
    }

    public void SetVisibility(bool isVisible)
    {
        if (_isVisible == isVisible)
        {
            return;
        }

        _logger.LogInformation("Tab visibility changed: isVisible={IsVisible}", isVisible);
        _isVisible = isVisible;

        if (!isVisible)
        {
            _timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
        else
        {
            _ = TriggerImmediateSyncAsync();
            _timer?.Change(SyncInterval, SyncInterval);
        }
    }

    private void HandleVisibilityChanged(bool isVisible)
    {
        SetVisibility(isVisible);
    }

    private void HandleRemoteTableChanged(string table)
    {
        if (table == "activity_events")
        {
            if (ShouldSuppressRealtimeEcho())
            {
                _logger.LogDebug("Ignoring realtime activity_events echo from recent local write.");
                return;
            }

            _logger.LogInformation("activity_events table changed via realtime, triggering sync.");
            _ = TriggerImmediateSyncAsync();
        }
    }

    private void HandleRemoteConnected()
    {
        if (!_isVisible)
        {
            _logger.LogDebug("Supabase Realtime connected while tab is in background. Deferring sync until tab becomes visible.");
            return;
        }

        _logger.LogInformation("Supabase Realtime connected, triggering catch-up sync.");
        _ = TriggerImmediateSyncAsync();
    }

    private void HandleLocalStateChanged()
    {
        if (_suppressNextLocalStateTriggeredSync)
        {
            _suppressNextLocalStateTriggeredSync = false;
            _logger.LogDebug("Ignoring local state change triggered by server-applied sync update.");
            return;
        }

        _logger.LogInformation("Local state changed, triggering sync.");
        _ = TriggerImmediateSyncAsync();
    }

    private void OnTimerElapsed(object? state)
    {
        if (!_isVisible)
        {
            _logger.LogDebug("Sync timer elapsed while tab in background. Skipping sync.");
            return;
        }

        _ = TriggerImmediateSyncAsync();
    }

    public ActivityEventQueryOptions GetDefaultSyncQueryOptions()
    {
        if (ActiveQueryOptionsProvider != null)
        {
            var custom = ActiveQueryOptionsProvider();
            if (custom != null)
            {
                return custom;
            }
        }

        var now = DateTimeOffset.UtcNow;
        var startOfYear = new DateTimeOffset(now.Year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        return new ActivityEventQueryOptions
        {
            From = startOfYear,
            To = now.AddDays(1)
        };
    }

    public async Task PullFromServerAsync(ActivityEventQueryOptions? options = null)
    {
        if (!await _authService.EnsureAuthenticatedAsync()) return;

        await _syncLock.WaitAsync();
        try
        {
            var effectiveOptions = options ?? GetDefaultSyncQueryOptions();
            var serverEvents = await _eventStore.LoadEventsAsync(effectiveOptions);
            ApplyServerEvents(serverEvents, effectiveOptions);
            _lastSyncedLocalEvents = MergeSnapshot(_lastSyncedLocalEvents, serverEvents, effectiveOptions);
            _lastSyncedServerEvents = MergeSnapshot(_lastSyncedServerEvents, serverEvents, effectiveOptions);
            _settingsService.UpdateLastSupabaseSync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pull events from server.");
        }
        finally
        {
            _syncLock.Release();
            OnSyncStateChanged?.Invoke();
        }
    }

    public async Task TriggerImmediateSyncAsync(ActivityEventQueryOptions? options = null)
    {
        if (!await _authService.EnsureAuthenticatedAsync()) return;

        await _syncLock.WaitAsync();

        try
        {
            var effectiveOptions = options ?? GetDefaultSyncQueryOptions();
            var allLocalEvents = _timeTrackingService.Account.Events.ToList();
            var scopedLocalEvents = FilterEvents(allLocalEvents, effectiveOptions);

            var serverEvents = await _eventStore.LoadEventsAsync(effectiveOptions);

            // Bootstrap sync state on first run with deterministic rules to avoid noisy conflict prompts.
            if (_lastSyncedLocalEvents is null || _lastSyncedServerEvents is null)
            {
                await HandleBootstrapSyncAsync(scopedLocalEvents, serverEvents, effectiveOptions);
                return;
            }

            // If local and server data in the scoped window are already identical,
            // there is no divergence. Establish/update snapshot for this window without triggering conflicts.
            if (!DetermineIfServerChanged(scopedLocalEvents, serverEvents))
            {
                _lastSyncedLocalEvents = MergeSnapshot(_lastSyncedLocalEvents, scopedLocalEvents, effectiveOptions);
                _lastSyncedServerEvents = MergeSnapshot(_lastSyncedServerEvents, serverEvents, effectiveOptions);
                return;
            }

            // Scope comparison baselines to the active query window so changing windows/views
            // does not falsely trigger conflict detection.
            var baseLocalEventsForComparison = FilterEvents(_lastSyncedLocalEvents, effectiveOptions);
            var baseEventsForComparison = FilterEvents(_lastSyncedServerEvents, effectiveOptions);

            bool hasLocalChanges = DetermineIfServerChanged(baseLocalEventsForComparison, scopedLocalEvents);
            bool hasServerChanges = DetermineIfServerChanged(baseEventsForComparison, serverEvents);

            if (hasLocalChanges && hasServerChanges)
            {
                _logger.LogWarning("Conflict detected. Local and server both have changes.");
                // We have a conflict. Ask the user.
                bool useServer = await ResolveConflictSafelyAsync();

                if (useServer)
                {
                    _suppressNextLocalStateTriggeredSync = true;
                    ApplyServerEvents(serverEvents, effectiveOptions);
                    _lastSyncedLocalEvents = MergeSnapshot(_lastSyncedLocalEvents, serverEvents, effectiveOptions);
                    _lastSyncedServerEvents = MergeSnapshot(_lastSyncedServerEvents, serverEvents, effectiveOptions);
                }
                else
                {
                    await _eventStore.SaveEventsAsync(scopedLocalEvents, effectiveOptions);
                    MarkRealtimeEchoSuppression();
                    _lastSyncedLocalEvents = MergeSnapshot(_lastSyncedLocalEvents, scopedLocalEvents, effectiveOptions);
                    _lastSyncedServerEvents = MergeSnapshot(_lastSyncedServerEvents, scopedLocalEvents, effectiveOptions);
                }
            }
            else if (hasServerChanges)
            {
                _logger.LogInformation("Server has changes. Overwriting local data.");
                _suppressNextLocalStateTriggeredSync = true;
                ApplyServerEvents(serverEvents, effectiveOptions);
                _lastSyncedLocalEvents = MergeSnapshot(_lastSyncedLocalEvents, serverEvents, effectiveOptions);
                _lastSyncedServerEvents = MergeSnapshot(_lastSyncedServerEvents, serverEvents, effectiveOptions);
            }
            else if (hasLocalChanges)
            {
                _logger.LogInformation("Local has changes. Pushing to server.");
                await _eventStore.SaveEventsAsync(scopedLocalEvents, effectiveOptions);
                MarkRealtimeEchoSuppression();

                _lastSyncedLocalEvents = MergeSnapshot(_lastSyncedLocalEvents, scopedLocalEvents, effectiveOptions);
                _lastSyncedServerEvents = MergeSnapshot(_lastSyncedServerEvents, scopedLocalEvents, effectiveOptions);
            }
            else
            {
                _logger.LogInformation("Unsynced baseline divergence detected. Reconciling with server snapshot.");
                _suppressNextLocalStateTriggeredSync = true;
                ApplyServerEvents(serverEvents, effectiveOptions);
                _lastSyncedLocalEvents = MergeSnapshot(_lastSyncedLocalEvents, serverEvents, effectiveOptions);
                _lastSyncedServerEvents = MergeSnapshot(_lastSyncedServerEvents, serverEvents, effectiveOptions);
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
            OnSyncStateChanged?.Invoke();
        }
    }

    private async Task HandleBootstrapSyncAsync(IReadOnlyList<ActivityEvent> localEvents, IReadOnlyList<ActivityEvent> serverEvents, ActivityEventQueryOptions? scopedWindow = null)
    {
        if (!DetermineIfServerChanged(localEvents, serverEvents))
        {
            _logger.LogInformation("Initial sync: local and server are identical. Baseline established.");
            _lastSyncedLocalEvents = MergeSnapshot(_lastSyncedLocalEvents, localEvents, scopedWindow);
            _lastSyncedServerEvents = MergeSnapshot(_lastSyncedServerEvents, serverEvents, scopedWindow);
            return;
        }

        // If the server has no events yet, seed it from local.
        if (serverEvents.Count == 0 && localEvents.Count > 0)
        {
            _logger.LogInformation("Initial sync: server is empty, seeding from local events.");
            await _eventStore.SaveEventsAsync(localEvents, scopedWindow);
            MarkRealtimeEchoSuppression();
            _lastSyncedLocalEvents = MergeSnapshot(_lastSyncedLocalEvents, localEvents, scopedWindow);
            _lastSyncedServerEvents = MergeSnapshot(_lastSyncedServerEvents, localEvents, scopedWindow);
            _settingsService.UpdateLastSupabaseSync();
            return;
        }

        // If both sides already have divergent data on first sync, ask the user instead of
        // silently preferring one side and risking unexpected data loss.
        if (localEvents.Count > 0 && serverEvents.Count > 0)
        {
            _logger.LogWarning("Initial sync: divergent local/server data detected, prompting conflict resolution.");
            bool useServer = await ResolveConflictSafelyAsync();

            if (useServer)
            {
                _suppressNextLocalStateTriggeredSync = true;
                ApplyServerEvents(serverEvents, scopedWindow);
                _lastSyncedLocalEvents = MergeSnapshot(_lastSyncedLocalEvents, serverEvents, scopedWindow);
                _lastSyncedServerEvents = MergeSnapshot(_lastSyncedServerEvents, serverEvents, scopedWindow);
            }
            else
            {
                await _eventStore.SaveEventsAsync(localEvents, scopedWindow);
                MarkRealtimeEchoSuppression();
                _lastSyncedLocalEvents = MergeSnapshot(_lastSyncedLocalEvents, localEvents, scopedWindow);
                _lastSyncedServerEvents = MergeSnapshot(_lastSyncedServerEvents, localEvents, scopedWindow);
            }

            _settingsService.UpdateLastSupabaseSync();
            return;
        }

        // In all other divergent bootstrap cases, prefer server snapshot to avoid accidental overwrite.
        _logger.LogInformation("Initial sync: divergent state detected, preferring server snapshot.");
        _suppressNextLocalStateTriggeredSync = true;
        ApplyServerEvents(serverEvents, scopedWindow);
        _lastSyncedLocalEvents = MergeSnapshot(_lastSyncedLocalEvents, serverEvents, scopedWindow);
        _lastSyncedServerEvents = MergeSnapshot(_lastSyncedServerEvents, serverEvents, scopedWindow);
        _settingsService.UpdateLastSupabaseSync();
    }

    private void ApplyServerEvents(IReadOnlyList<ActivityEvent> serverEvents, ActivityEventQueryOptions? options)
    {
        if (options != null && (options.From.HasValue || options.To.HasValue || options.Since.HasValue))
        {
            _timeTrackingService.MergeEventsFromServer(serverEvents, options.From, options.To);
        }
        else
        {
            _timeTrackingService.UpdateEventsFromServer(serverEvents);
        }
    }

    private static List<ActivityEvent> FilterEvents(IEnumerable<ActivityEvent>? events, ActivityEventQueryOptions? options)
    {
        if (events == null) return new List<ActivityEvent>();
        if (options == null) return events.ToList();

        var query = events;
        if (options.From.HasValue)
        {
            query = query.Where(e => e.StartTime >= options.From.Value);
        }
        if (options.To.HasValue)
        {
            query = query.Where(e => e.StartTime <= options.To.Value);
        }
        return query.ToList();
    }

    private static List<ActivityEvent> MergeSnapshot(
        IReadOnlyList<ActivityEvent>? currentSnapshot,
        IReadOnlyList<ActivityEvent> windowEvents,
        ActivityEventQueryOptions? options)
    {
        if (currentSnapshot == null)
        {
            return windowEvents.Select(e => e.Clone()).ToList();
        }

        if (options?.From == null && options?.To == null)
        {
            if (options?.Since != null)
            {
                var windowDict = new Dictionary<Guid, ActivityEvent>();
                foreach (var evt in windowEvents)
                {
                    windowDict[evt.Id] = evt;
                }

                return currentSnapshot
                    .Where(e => !windowDict.ContainsKey(e.Id))
                    .Concat(windowEvents)
                    .Select(e => e.Clone())
                    .OrderBy(e => e.StartTime)
                    .ToList();
            }

            return windowEvents.Select(e => e.Clone()).ToList();
        }

        var retained = currentSnapshot
            .Where(e => (options.From != null && e.StartTime < options.From.Value) ||
                        (options.To != null && e.StartTime > options.To.Value))
            .ToList();

        return retained
            .Concat(windowEvents)
            .Select(e => e.Clone())
            .OrderBy(e => e.StartTime)
            .ToList();
    }

    private async Task<bool> ResolveConflictSafelyAsync()
    {
        if (_conflictNotifier is SyncConflictNotifier concreteNotifier && !concreteNotifier.HasListeners)
        {
            _logger.LogWarning("Conflict detected without any UI listener. Keeping local data to avoid silent overwrite.");
            return false;
        }

        return await _conflictNotifier.ResolveConflictAsync();
    }

    private void MarkRealtimeEchoSuppression()
    {
        _suppressRealtimeUntilUtc = DateTimeOffset.UtcNow.Add(RealtimeEchoSuppressionWindow);
    }

    private bool ShouldSuppressRealtimeEcho()
    {
        if (_suppressRealtimeUntilUtc is not { } suppressUntil)
        {
            return false;
        }

        if (DateTimeOffset.UtcNow <= suppressUntil)
        {
            return true;
        }

        _suppressRealtimeUntilUtc = null;
        return false;
    }

    private bool DetermineIfServerChanged(IReadOnlyList<ActivityEvent> baseState, IReadOnlyList<ActivityEvent> server)
    {
        // Simple heuristic: if count differs, it changed.
        if (baseState.Count != server.Count) return true;

        // More advanced: check if any event IDs differ or if the fields differ
        var baseDict = new Dictionary<Guid, ActivityEvent>();
        foreach (var b in baseState)
        {
            baseDict[b.Id] = b;
        }

        foreach (var s in server)
        {
            if (!baseDict.TryGetValue(s.Id, out var b)) return true; // new event on server

            if (AreEventsDifferent(b, s)) return true;
        }

        return false;
    }

    private static bool AreEventsDifferent(ActivityEvent a, ActivityEvent b)
    {
        // Supabase/PostgreSQL timestamps might have different sub-millisecond precision,
        // so we tolerate small differences (e.g., < 1 ms) in StartTime and EndTime
        if (Math.Abs((a.StartTime - b.StartTime).TotalMilliseconds) >= 1) return true;

        if (a.EndTime.HasValue != b.EndTime.HasValue) return true;
        if (a.EndTime.HasValue && b.EndTime.HasValue &&
            Math.Abs((a.EndTime.Value - b.EndTime.Value).TotalMilliseconds) >= 1) return true;

        if ((a.Comment ?? string.Empty) != (b.Comment ?? string.Empty)) return true;
        if ((a.ActivityName ?? string.Empty) != (b.ActivityName ?? string.Empty)) return true;
        if ((a.ActivityColor ?? string.Empty) != (b.ActivityColor ?? string.Empty)) return true;
        if ((a.ActivityEmoji ?? string.Empty) != (b.ActivityEmoji ?? string.Empty)) return true;
        if (a.ActivityId != b.ActivityId) return true;

        return false;
    }

    public bool IsEventSynchronized(ActivityEvent evt)
    {
        if (_lastSyncedServerEvents == null) return false;

        var serverEvt = _lastSyncedServerEvents.FirstOrDefault(e => e.Id == evt.Id);
        if (serverEvt == null) return false;

        return !AreEventsDifferent(evt, serverEvt);
    }

    public void Dispose()
    {
        if (_pwaService is not null)
        {
            _pwaService.OnVisibilityChanged -= HandleVisibilityChanged;
        }

        if (_realtimeService is not null)
        {
            _realtimeService.OnTableChanged -= HandleRemoteTableChanged;
            _realtimeService.OnConnected -= HandleRemoteConnected;
        }

        if (_timeTrackingService is not null)
        {
            _timeTrackingService.OnStateChanged -= HandleLocalStateChanged;
        }

        _timer?.Dispose();
        _syncLock.Dispose();
    }
}
