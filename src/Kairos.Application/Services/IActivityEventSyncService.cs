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
    /// Triggers an immediate sync (e.g. after a local change).
    /// </summary>
    Task TriggerImmediateSyncAsync();

    /// <summary>
    /// Triggers an immediate pull from the server, discarding local changes if requested.
    /// </summary>
    Task PullFromServerAsync();

    /// <summary>
    /// Event fired when the synchronization state changes.
    /// </summary>
    event Action? OnSyncStateChanged;

    /// <summary>
    /// Determines whether the given event is synchronized with the server.
    /// </summary>
    bool IsEventSynchronized(ActivityEvent evt);
}
