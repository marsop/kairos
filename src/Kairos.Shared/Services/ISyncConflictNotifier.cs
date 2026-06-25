namespace Kairos.Shared.Services;

/// <summary>
/// Allows the background sync service to prompt the user to resolve data conflicts.
/// </summary>
public interface ISyncConflictNotifier
{
    /// <summary>
    /// Event raised when a conflict occurs.
    /// The argument is an action the UI can call to indicate the user's choice:
    /// true to overwrite local with server, false to overwrite server with local.
    /// </summary>
    event Action<Action<bool>>? OnConflictDetected;

    /// <summary>
    /// Triggers a conflict resolution prompt.
    /// Returns true if server data should overwrite local data,
    /// false if local data should overwrite server data.
    /// </summary>
    Task<bool> ResolveConflictAsync();
}
