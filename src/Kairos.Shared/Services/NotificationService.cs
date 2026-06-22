using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Kairos.Shared.Services;

/// <summary>
/// Implementation of notification service: in-app toasts + optional browser notifications.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<NotificationService> _logger;

    public event Action<ToastMessage>? OnToastReceived;

    public NotificationService(IJSRuntime jsRuntime, ISettingsService settingsService, ILogger<NotificationService> logger)
    {
        _jsRuntime = jsRuntime;
        _settingsService = settingsService;
        _logger = logger;
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
                _logger.LogWarning(ex, "Browser notification failed.");
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
