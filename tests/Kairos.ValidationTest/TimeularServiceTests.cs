using Kairos.Core.Models;
using Kairos.Application.Services;
using Kairos.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kairos.ValidationTest;

public class TimeularServiceTests
{
    [Fact]
    public async Task OnTimeularChange_OrientationFaceOne_ActivatesFirstActivityAndLogs()
    {
        var settings = new StubSettingsService();
        var timeTracking = await CreateLoadedTimeTrackingServiceAsync(settings);
        var activityPrompt = new ActivityStartPromptService(timeTracking, new StubStringLocalizer());
        var notifications = new StubNotificationService();
        var sut = new TimeularService(
            new TestJsRuntime(),
            timeTracking,
            activityPrompt,
            notifications,
            settings,
            new StubStringLocalizer(),
            NullLogger<TimeularService>.Instance);

        await sut.OnTimeularChange(new TimeularService.TimeularChangeEvent
        {
            EventType = "orientation",
            Face = 1,
            RawHex = "0x01"
        });

        Assert.Null(timeTracking.GetActiveEvent());
        Assert.Equal(timeTracking.Account.Activities.OrderBy(m => m.DisplayOrder).First().Id, activityPrompt.PendingActivityId);
        Assert.NotEmpty(sut.ChangeLog);
        Assert.Contains("comment requested for #1", sut.ChangeLog[0].Message);
    }

    [Fact]
    public async Task OnTimeularChange_UnknownFace_DeactivatesCurrentActivity()
    {
        var settings = new StubSettingsService();
        var timeTracking = await CreateLoadedTimeTrackingServiceAsync(settings);
        var firstActivity = timeTracking.Account.Activities.OrderBy(m => m.DisplayOrder).First();
        timeTracking.ActivateActivity(firstActivity.Id, "Manual");
        var sut = new TimeularService(
            new TestJsRuntime(),
            timeTracking,
            new ActivityStartPromptService(timeTracking, new StubStringLocalizer()),
            new StubNotificationService(),
            settings,
            new StubStringLocalizer(),
            NullLogger<TimeularService>.Instance);

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
        var settings = new StubSettingsService();
        var timeTracking = await CreateLoadedTimeTrackingServiceAsync(settings);
        var sut = new TimeularService(
            new TestJsRuntime(),
            timeTracking,
            new ActivityStartPromptService(timeTracking, new StubStringLocalizer()),
            notifications,
            settings,
            new StubStringLocalizer(),
            NullLogger<TimeularService>.Instance);

        await sut.OnTimeularChange(new TimeularService.TimeularChangeEvent { EventType = "disconnected" });

        Assert.False(sut.IsConnected);
        Assert.Equal("error", sut.StatusClass);
        Assert.Equal("Timeular disconnected.", sut.StatusMessage);
        Assert.Contains(notifications.SentNotifications, n => n.Title == "NotificationTimeularDisconnectedTitle");
    }

    [Fact]
    public async Task OnTimeularChange_GroupsDisabled_MapsAcrossBothGroupsInSingleList()
    {
        var settings = new StubSettingsService
        {
            ActivityGroupsEnabled = false,
            ActiveActivityGroup = 0
        };

        var groupedActivities = new[]
        {
            new Activity { Name = "Group0-A", DisplayOrder = 0, ActivityGroupId = 0 },
            new Activity { Name = "Group1-A", DisplayOrder = 0, ActivityGroupId = 1 }
        };

        var timeTracking = await CreateLoadedTimeTrackingServiceAsync(settings, groupedActivities);
        var activityPrompt = new ActivityStartPromptService(timeTracking, new StubStringLocalizer());
        var sut = new TimeularService(
            new TestJsRuntime(),
            timeTracking,
            activityPrompt,
            new StubNotificationService(),
            settings,
            new StubStringLocalizer(),
            NullLogger<TimeularService>.Instance);

        await sut.OnTimeularChange(new TimeularService.TimeularChangeEvent
        {
            EventType = "orientation",
            Face = 2,
            RawHex = "0x02"
        });

        var expectedSecondActivityId = timeTracking.Account.Activities
            .OrderBy(a => a.ActivityGroupId)
            .ThenBy(a => a.DisplayOrder)
            .Skip(1)
            .First()
            .Id;

        Assert.Equal(expectedSecondActivityId, activityPrompt.PendingActivityId);
        Assert.Contains("comment requested for #2", sut.ChangeLog[0].Message);
    }

    private static async Task<TimeTrackingService> CreateLoadedTimeTrackingServiceAsync(
        StubSettingsService settings,
        IEnumerable<Activity>? activities = null)
    {
        var storage = new InMemoryStorageService();
        var seedActivities = activities ??
        [
            new Activity { Name = "Work", DisplayOrder = 0, ActivityGroupId = 0 },
            new Activity { Name = "Break", DisplayOrder = 1, ActivityGroupId = 0 }
        ];
        var config = new StubActivityConfigurationService(seedActivities);
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
