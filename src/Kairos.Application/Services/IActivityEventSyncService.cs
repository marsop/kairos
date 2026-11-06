using Kairos.Core.Models;

namespace Kairos.Application.Services;

/// <summary>
/// Service responsible for keeping ActivityEvents synchronized with the server
/// on a periodic basis and resolving conflicts.
/// </summary>
public interface IActivityEventSyncService
{
    /// <summary>
    /// Initializes the sync timer and starts syncing.
    /// </summary>
    void StartSync();

    /// <summary>
    /// Gets or sets an optional provider to dynamically determine query bounds (e.g. active viewing window).
    /// </summary>
    Func<ActivityEventQueryOptions?>? ActiveQueryOptionsProvider { get; set; }

    /// <summary>
    /// Triggers an immediate sync (e.g. after a local change).
    /// </summary>
    Task TriggerImmediateSyncAsync(ActivityEventQueryOptions? options = null);

    /// <summary>
    /// Triggers an immediate pull from the server, discarding local changes if requested.
    /// </summary>
    Task PullFromServerAsync(ActivityEventQueryOptions? options = null);

    /// <summary>
    /// Event fired when the synchronization state changes.
    /// </summary>
    event Action? OnSyncStateChanged;

    /// <summary>
    /// Determines whether the given event is synchronized with the server.
    /// </summary>
    bool IsEventSynchronized(ActivityEvent evt);

    /// <summary>
    /// Notifies the sync service of tab/window visibility state.
    /// Pauses background periodic sync when hidden, resumes and triggers immediate sync when visible.
    /// </summary>
    void SetVisibility(bool isVisible);
}
