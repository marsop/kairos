namespace Budgetr.Shared.Services;

/// <summary>
/// Represents a toast message to display in the UI.
/// </summary>
public record ToastMessage(string Title, string Body, DateTimeOffset CreatedAt);

/// <summary>
/// Interface for notification services (in-app toast + optional browser notifications).
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Sends a notification (always shows in-app toast, optionally browser notification).
    /// </summary>
    Task NotifyAsync(string title, string body);

    /// <summary>
    /// Gets the current browser notification permission state ("default", "granted", "denied").
    /// </summary>
    Task<string> GetBrowserPermissionStateAsync();

    /// <summary>
    /// Requests browser notification permission from the user.
    /// </summary>
    /// <returns>The resulting permission state.</returns>
    Task<string> RequestBrowserPermissionAsync();

    /// <summary>
    /// Event raised when a new toast should be displayed.
    /// </summary>
    event Action<ToastMessage>? OnToastReceived;
}
