using Kairos.Shared.Models;
using Kairos.Shared.Services;

namespace Kairos.ValidationTest;

public class TimeularServiceTests
{
    [Fact]
    public async Task OnTimeularChange_OrientationFaceOne_ActivatesFirstActivityAndLogs()
    {
        var timeTracking = await CreateLoadedTimeTrackingServiceAsync();
        var notifications = new StubNotificationService();
        var sut = new TimeularService(new TestJsRuntime(), timeTracking, notifications, new StubStringLocalizer());

        await sut.OnTimeularChange(new TimeularService.TimeularChangeEvent
        {
            EventType = "orientation",
            Face = 1,
            RawHex = "0x01"
        });

        var active = timeTracking.GetActiveEvent();
        Assert.NotNull(active);
        Assert.Equal(timeTracking.Account.Activities.OrderBy(m => m.DisplayOrder).First().Name, active!.ActivityName);
        Assert.NotEmpty(sut.ChangeLog);
        Assert.Contains("activated #1", sut.ChangeLog[0].Message);
    }

    [Fact]
    public async Task OnTimeularChange_UnknownFace_DeactivatesCurrentActivity()
    {
        var timeTracking = await CreateLoadedTimeTrackingServiceAsync();
        var firstActivity = timeTracking.Account.Activities.OrderBy(m => m.DisplayOrder).First();
        timeTracking.ActivateActivity(firstActivity.Id, "Manual");
        var sut = new TimeularService(new TestJsRuntime(), timeTracking, new StubNotificationService(), new StubStringLocalizer());

        await sut.OnTimeularChange(new TimeularService.TimeularChangeEvent
        {
            EventType = "orientation",
            Face = 99,
            RawHex = "0x99"
        });

        Assert.Null(timeTracking.GetActiveEvent());
        Assert.Contains("deactivated", sut.ChangeLog[0].Message);
    }

    [Fact]
    public async Task OnTimeularChange_Disconnected_UpdatesStatusAndSendsNotification()
    {
        var notifications = new StubNotificationService();
        var sut = new TimeularService(
            new TestJsRuntime(),
            await CreateLoadedTimeTrackingServiceAsync(),
            notifications,
            new StubStringLocalizer());

        await sut.OnTimeularChange(new TimeularService.TimeularChangeEvent { EventType = "disconnected" });

        Assert.False(sut.IsConnected);
        Assert.Equal("error", sut.StatusClass);
        Assert.Equal("Timeular disconnected.", sut.StatusMessage);
        Assert.Contains(notifications.SentNotifications, n => n.Title == "NotificationTimeularDisconnectedTitle");
    }

    private static async Task<TimeTrackingService> CreateLoadedTimeTrackingServiceAsync()
    {
        var storage = new InMemoryStorageService();
        var config = new StubActivityConfigurationService(new[]
        {
            new Activity { Name = "Work", Factor = 1, DisplayOrder = 0 },
            new Activity { Name = "Break", Factor = 1, DisplayOrder = 1 }
        });
        var settings = new StubSettingsService();
        var notifications = new StubNotificationService();
        var service = new TimeTrackingService(
            storage,
            config,
            settings,
            notifications,
            new StubStringLocalizer(),
            new StubSupabaseAuthService(),
            new StubSupabaseActivityStore());
        await service.LoadAsync();
        return service;
    }
}
