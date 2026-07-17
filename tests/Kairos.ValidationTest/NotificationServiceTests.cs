using Microsoft.Extensions.Logging.Abstractions;
using Kairos.Application.Services;
using Kairos.Infrastructure.Services;
using Microsoft.JSInterop;

namespace Kairos.ValidationTest;

public class NotificationServiceTests
{
    [Fact]
    public async Task NotifyAsync_AlwaysRaisesToast()
    {
        var js = new TestJsRuntime();
        var settings = new StubSettingsService { BrowserNotificationsEnabled = false };
        var sut = new NotificationService(js, settings, NullLogger<NotificationService>.Instance);
        ToastMessage? toast = null;
        sut.OnToastReceived += message => toast = message;

        await sut.NotifyAsync("Title", "Body");

        Assert.NotNull(toast);
        Assert.Equal("Title", toast!.Title);
        Assert.Equal("Body", toast.Body);
        Assert.DoesNotContain(js.Invocations, x => x.Identifier == "notificationInterop.showNotification");
    }

    [Fact]
    public async Task NotifyAsync_BrowserEnabledAndPermissionGranted_ShowsBrowserNotification()
    {
        var js = new TestJsRuntime();
        js.SetResult("notificationInterop.getPermissionState", "granted");
        var settings = new StubSettingsService { BrowserNotificationsEnabled = true };
        var sut = new NotificationService(js, settings, NullLogger<NotificationService>.Instance);

        await sut.NotifyAsync("Connected", "Timeular connected");

        Assert.Contains(js.Invocations, x => x.Identifier == "notificationInterop.showNotification");
    }

    [Fact]
    public async Task GetBrowserPermissionStateAsync_WhenJsThrows_ReturnsDenied()
    {
        var js = new TestJsRuntime();
        js.SetException("notificationInterop.getPermissionState", new JSException("boom"));
        var sut = new NotificationService(js, new StubSettingsService(), NullLogger<NotificationService>.Instance);

        var state = await sut.GetBrowserPermissionStateAsync();

        Assert.Equal("denied", state);
    }
}
