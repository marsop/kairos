using Kairos.Shared.Models;
using Microsoft.Extensions.Logging;

namespace Kairos.Shared.Services;

public sealed class ActivityEventSyncService : IActivityEventSyncService, IDisposable
{
    private static readonly TimeSpan RealtimeEchoSuppressionWindow = TimeSpan.FromSeconds(2);

    private readonly ISupabaseActivityEventStore _eventStore;
    private readonly ITimeTrackingService _timeTrackingService;
    private readonly ISupabaseAuthService _authService;
    private readonly ISyncConflictNotifier _conflictNotifier;
    private readonly ISettingsService _settingsService;
    private readonly ISupabaseRealtimeService _realtimeService;
    private readonly ILogger<ActivityEventSyncService> _logger;
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private Timer? _timer;

    // Track the last known event snapshots to detect local/server divergence.
    // Using event content instead of LastModified avoids false positives from unrelated local state changes.
    private IReadOnlyList<ActivityEvent>? _lastSyncedLocalEvents;
    private IReadOnlyList<ActivityEvent>? _lastSyncedServerEvents;
    private DateTimeOffset? _suppressRealtimeUntilUtc;
    private bool _suppressNextLocalStateTriggeredSync;

    public event Action? OnSyncStateChanged;

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
        _timeTrackingService.OnStateChanged += HandleLocalStateChanged;
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
            _lastSyncedLocalEvents = serverEvents.Select(e => e.Clone()).ToList();
            _lastSyncedServerEvents = serverEvents.Select(e => e.Clone()).ToList();
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

    public async Task TriggerImmediateSyncAsync()
    {
        if (!_authService.IsAuthenticated) return;

        await _syncLock.WaitAsync();

        try
        {
            var localEvents = _timeTrackingService.Account.Events.ToList();

            var serverEvents = await _eventStore.LoadEventsAsync();

            // Bootstrap sync state on first run with deterministic rules to avoid noisy conflict prompts.
            if (_lastSyncedLocalEvents is null || _lastSyncedServerEvents is null)
            {
                await HandleBootstrapSyncAsync(localEvents, serverEvents);
                return;
            }

            // Detect local event changes against the last known local snapshot if available.
            // Fallback to server comparison during bootstrap when no local snapshot exists yet.
            var baseLocalEventsForComparison = _lastSyncedLocalEvents ?? _lastSyncedServerEvents ?? serverEvents;
            bool hasLocalChanges = DetermineIfServerChanged(baseLocalEventsForComparison, localEvents);

            // For a robust sync, we would check if server data is different.
            // Since we don't have a reliable LastModifiedAtUtc on the server side easily accessible here
            // without pulling the whole list, we pull it and do a hash/count check.
            // Use the last synced server events snapshot if available, otherwise fallback to local events
            var baseEventsForComparison = _lastSyncedServerEvents ?? localEvents;
            bool hasServerChanges = DetermineIfServerChanged(baseEventsForComparison, serverEvents);

            if (hasLocalChanges && hasServerChanges)
            {
                _logger.LogWarning("Conflict detected. Local and server both have changes.");
                // We have a conflict. Ask the user.
                bool useServer = await ResolveConflictSafelyAsync();

                if (useServer)
                {
                    _suppressNextLocalStateTriggeredSync = true;
                    _timeTrackingService.UpdateEventsFromServer(serverEvents);
                    _lastSyncedLocalEvents = serverEvents.Select(e => e.Clone()).ToList();
                    _lastSyncedServerEvents = serverEvents.Select(e => e.Clone()).ToList();
                }
                else
                {
                    await _eventStore.SaveEventsAsync(localEvents);
                    MarkRealtimeEchoSuppression();
                    _lastSyncedLocalEvents = localEvents.Select(e => e.Clone()).ToList();
                    _lastSyncedServerEvents = localEvents.Select(e => e.Clone()).ToList();
                }
            }
            else if (hasServerChanges)
            {
                _logger.LogInformation("Server has changes. Overwriting local data.");
                _suppressNextLocalStateTriggeredSync = true;
                _timeTrackingService.UpdateEventsFromServer(serverEvents);
                _lastSyncedLocalEvents = serverEvents.Select(e => e.Clone()).ToList();
                _lastSyncedServerEvents = serverEvents.Select(e => e.Clone()).ToList();
            }
            else if (hasLocalChanges)
            {
                _logger.LogInformation("Local has changes. Pushing to server.");
                await _eventStore.SaveEventsAsync(localEvents);
                MarkRealtimeEchoSuppression();

                _lastSyncedLocalEvents = localEvents.Select(e => e.Clone()).ToList();
                _lastSyncedServerEvents = localEvents.Select(e => e.Clone()).ToList();
            }
            else
            {
                // No changes, but we might be on first sync where snapshot is null
                _lastSyncedLocalEvents ??= localEvents.Select(e => e.Clone()).ToList();
                _lastSyncedServerEvents ??= serverEvents.Select(e => e.Clone()).ToList();
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

    private async Task HandleBootstrapSyncAsync(IReadOnlyList<ActivityEvent> localEvents, IReadOnlyList<ActivityEvent> serverEvents)
    {
        if (!DetermineIfServerChanged(localEvents, serverEvents))
        {
            _logger.LogInformation("Initial sync: local and server are identical. Baseline established.");
            _lastSyncedLocalEvents = localEvents.Select(e => e.Clone()).ToList();
            _lastSyncedServerEvents = serverEvents.Select(e => e.Clone()).ToList();
            return;
        }

        // If the server has no events yet, seed it from local.
        if (serverEvents.Count == 0 && localEvents.Count > 0)
        {
            _logger.LogInformation("Initial sync: server is empty, seeding from local events.");
            await _eventStore.SaveEventsAsync(localEvents);
            MarkRealtimeEchoSuppression();
            _lastSyncedLocalEvents = localEvents.Select(e => e.Clone()).ToList();
            _lastSyncedServerEvents = localEvents.Select(e => e.Clone()).ToList();
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
                _timeTrackingService.UpdateEventsFromServer(serverEvents);
                _lastSyncedLocalEvents = serverEvents.Select(e => e.Clone()).ToList();
                _lastSyncedServerEvents = serverEvents.Select(e => e.Clone()).ToList();
            }
            else
            {
                await _eventStore.SaveEventsAsync(localEvents);
                MarkRealtimeEchoSuppression();
                _lastSyncedLocalEvents = localEvents.Select(e => e.Clone()).ToList();
                _lastSyncedServerEvents = localEvents.Select(e => e.Clone()).ToList();
            }

            _settingsService.UpdateLastSupabaseSync();
            return;
        }

        // In all other divergent bootstrap cases, prefer server snapshot to avoid accidental overwrite.
        _logger.LogInformation("Initial sync: divergent state detected, preferring server snapshot.");
        _suppressNextLocalStateTriggeredSync = true;
        _timeTrackingService.UpdateEventsFromServer(serverEvents);
        _lastSyncedLocalEvents = serverEvents.Select(e => e.Clone()).ToList();
        _lastSyncedServerEvents = serverEvents.Select(e => e.Clone()).ToList();
        _settingsService.UpdateLastSupabaseSync();
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
        var baseDict = baseState.ToDictionary(e => e.Id);
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

        if (a.Comment != b.Comment) return true;
        if (a.ActivityName != b.ActivityName) return true;
        if (a.ActivityColor != b.ActivityColor) return true;
        if (a.ActivityEmoji != b.ActivityEmoji) return true;
        if (a.ActivityId != b.ActivityId) return true;
        if (a.Metadata != b.Metadata) return true;

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
