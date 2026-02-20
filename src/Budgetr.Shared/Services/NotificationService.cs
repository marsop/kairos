using Microsoft.JSInterop;

namespace Budgetr.Shared.Services;

/// <summary>
/// Implementation of notification service: in-app toasts + optional browser notifications.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ISettingsService _settingsService;

    public event Action<ToastMessage>? OnToastReceived;

    public NotificationService(IJSRuntime jsRuntime, ISettingsService settingsService)
    {
        _jsRuntime = jsRuntime;
        _settingsService = settingsService;
    }

    public async Task NotifyAsync(string title, string body)
    {
        // Always fire in-app toast
        OnToastReceived?.Invoke(new ToastMessage(title, body, DateTimeOffset.UtcNow));

        // Optionally fire browser notification
        if (_settingsService.BrowserNotificationsEnabled)
        {
            try
            {
                var permission = await GetBrowserPermissionStateAsync();
                if (permission == "granted")
                {
                    await _jsRuntime.InvokeVoidAsync("notificationInterop.showNotification", title, body);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NotificationService] Browser notification failed: {ex.Message}");
            }
        }
    }

    public async Task<string> GetBrowserPermissionStateAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string>("notificationInterop.getPermissionState");
        }
        catch
        {
            return "denied";
        }
    }

    public async Task<string> RequestBrowserPermissionAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string>("notificationInterop.requestPermission");
        }
        catch
        {
            return "denied";
        }
    }
}
