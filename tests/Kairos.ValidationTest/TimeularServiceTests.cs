using Kairos.Core.Models;
using Kairos.Application.Services;
using Kairos.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kairos.ValidationTest;

public class TimeularServiceTests
{
    [Fact]
    public async Task InitializeAsync_WithSavedStateAndSuccessfulReconnect_RestoresState()
    {
        var js = new TestJsRuntime();
        js.SetResult("timeularInterop.getSavedState", new TimeularService.TimeularSavedState
        {
            DeviceName = "My Timeular",
            DeviceId = "dev-123",
            ConnectedAtUtc = DateTimeOffset.UtcNow.ToString("O")
        });
        js.SetResult("timeularInterop.reconnectSavedDevice", new TimeularService.TimeularReconnectResult
        {
            Attempted = true,
            Success = true,
            DeviceName = "My Timeular",
            BatteryLevel = 88
        });

        var settings = new StubSettingsService();
        var timeTracking = await CreateLoadedTimeTrackingServiceAsync(settings);
        var notifications = new StubNotificationService();
        var sut = new TimeularService(
            js,
            timeTracking,
            new ActivityStartPromptService(timeTracking, new StubStringLocalizer()),
            notifications,
            settings,
            new StubStringLocalizer(),
            NullLogger<TimeularService>.Instance);

        await sut.InitializeAsync();

        Assert.True(sut.IsInitialized);
        Assert.True(sut.IsConnected);
        Assert.True(sut.HasConnectedBefore);
        Assert.Equal("My Timeular", sut.DeviceName);
        Assert.Equal(88, sut.BatteryLevel);
        Assert.Equal("success", sut.StatusClass);
        Assert.Contains(notifications.SentNotifications, n => n.Title == "NotificationTimeularConnectedTitle");
    }

    [Fact]
    public async Task InitializeAsync_WhenReconnectFails_RemainsDisconnected()
    {
        var js = new TestJsRuntime();
        js.SetResult("timeularInterop.getSavedState", new TimeularService.TimeularSavedState
        {
            DeviceName = "My Timeular",
            DeviceId = "dev-123"
        });
        js.SetResult("timeularInterop.reconnectSavedDevice", new TimeularService.TimeularReconnectResult
        {
            Attempted = true,
            Success = false,
            Message = "Device out of range"
        });

        var settings = new StubSettingsService();
        var timeTracking = await CreateLoadedTimeTrackingServiceAsync(settings);
        var sut = new TimeularService(
            js,
            timeTracking,
            new ActivityStartPromptService(timeTracking, new StubStringLocalizer()),
            new StubNotificationService(),
            settings,
            new StubStringLocalizer(),
            NullLogger<TimeularService>.Instance);

        await sut.InitializeAsync();

        Assert.True(sut.IsInitialized);
        Assert.False(sut.IsConnected);
        Assert.True(sut.HasConnectedBefore);
    }

    [Fact]
    public async Task ConnectAsync_UsingPicker_WhenSucceeds_ConnectsAndNotifies()
    {
        var js = new TestJsRuntime();
        js.SetResult("timeularInterop.requestAndConnect", new TimeularService.TimeularConnectResult
        {
            Success = true,
            DeviceName = "Picked Device",
            BatteryLevel = 95
        });

        var settings = new StubSettingsService();
        var timeTracking = await CreateLoadedTimeTrackingServiceAsync(settings);
        var notifications = new StubNotificationService();
        var sut = new TimeularService(
            js,
            timeTracking,
            new ActivityStartPromptService(timeTracking, new StubStringLocalizer()),
            notifications,
            settings,
            new StubStringLocalizer(),
            NullLogger<TimeularService>.Instance);

        await sut.ConnectAsync();

        Assert.True(sut.IsConnected);
        Assert.Equal("Picked Device", sut.DeviceName);
        Assert.Equal(95, sut.BatteryLevel);
        Assert.Equal("success", sut.StatusClass);
        Assert.Contains(notifications.SentNotifications, n => n.Title == "NotificationTimeularConnectedTitle");
    }

    [Fact]
    public async Task ConnectAsync_UsingPicker_WhenFails_SetsErrorStatus()
    {
        var js = new TestJsRuntime();
        js.SetResult("timeularInterop.requestAndConnect", new TimeularService.TimeularConnectResult
        {
            Success = false,
            Message = "User cancelled Bluetooth dialog"
        });

        var settings = new StubSettingsService();
        var timeTracking = await CreateLoadedTimeTrackingServiceAsync(settings);
        var sut = new TimeularService(
            js,
            timeTracking,
            new ActivityStartPromptService(timeTracking, new StubStringLocalizer()),
            new StubNotificationService(),
            settings,
            new StubStringLocalizer(),
            NullLogger<TimeularService>.Instance);

        await sut.ConnectAsync();

        Assert.False(sut.IsConnected);
        Assert.Equal("error", sut.StatusClass);
        Assert.Equal("User cancelled Bluetooth dialog", sut.StatusMessage);
    }

    [Fact]
    public async Task ConnectAsync_WhenExceptionThrown_HandlesGracefully()
    {
        var js = new TestJsRuntime();
        js.SetException("timeularInterop.requestAndConnect", new InvalidOperationException("Bluetooth not supported"));

        var settings = new StubSettingsService();
        var timeTracking = await CreateLoadedTimeTrackingServiceAsync(settings);
        var sut = new TimeularService(
            js,
            timeTracking,
            new ActivityStartPromptService(timeTracking, new StubStringLocalizer()),
            new StubNotificationService(),
            settings,
            new StubStringLocalizer(),
            NullLogger<TimeularService>.Instance);

        await sut.ConnectAsync();

        Assert.False(sut.IsConnected);
        Assert.Equal("error", sut.StatusClass);
        Assert.Contains("Bluetooth not supported", sut.StatusMessage);
    }

    [Fact]
    public async Task AutoConnectAsync_WhenAutoReconnectSucceeds_ConnectsWithoutPicker()
    {
        var js = new TestJsRuntime();
        js.SetResult("timeularInterop.reconnectSavedDevice", new TimeularService.TimeularReconnectResult
        {
            Success = true,
            DeviceName = "Auto Device"
        });

        var settings = new StubSettingsService();
        var timeTracking = await CreateLoadedTimeTrackingServiceAsync(settings);
        var sut = new TimeularService(
            js,
            timeTracking,
            new ActivityStartPromptService(timeTracking, new StubStringLocalizer()),
            new StubNotificationService(),
            settings,
            new StubStringLocalizer(),
            NullLogger<TimeularService>.Instance);

        await sut.AutoConnectAsync();

        Assert.True(sut.IsConnected);
        Assert.Equal("Auto Device", sut.DeviceName);
        Assert.DoesNotContain(js.Invocations, x => x.Identifier == "timeularInterop.requestAndConnect");
    }

    [Fact]
    public async Task DisconnectAsync_DisconnectsAndSendsNotification()
    {
        var js = new TestJsRuntime();
        var settings = new StubSettingsService();
        var timeTracking = await CreateLoadedTimeTrackingServiceAsync(settings);
        var notifications = new StubNotificationService();
        var sut = new TimeularService(
            js,
            timeTracking,
            new ActivityStartPromptService(timeTracking, new StubStringLocalizer()),
            notifications,
            settings,
            new StubStringLocalizer(),
            NullLogger<TimeularService>.Instance);

        await sut.DisconnectAsync();

        Assert.False(sut.IsConnected);
        Assert.Null(sut.BatteryLevel);
        Assert.Contains(js.Invocations, x => x.Identifier == "timeularInterop.disconnect");
        Assert.Contains(notifications.SentNotifications, n => n.Title == "NotificationTimeularDisconnectedTitle");
    }

    [Fact]
    public async Task DisconnectAsync_WhenExceptionThrown_SetsErrorStatus()
    {
        var js = new TestJsRuntime();
        js.SetException("timeularInterop.disconnect", new InvalidOperationException("Disconnect error"));
        var settings = new StubSettingsService();
        var timeTracking = await CreateLoadedTimeTrackingServiceAsync(settings);
        var sut = new TimeularService(
            js,
            timeTracking,
            new ActivityStartPromptService(timeTracking, new StubStringLocalizer()),
            new StubNotificationService(),
            settings,
            new StubStringLocalizer(),
            NullLogger<TimeularService>.Instance);

        await sut.DisconnectAsync();

        Assert.Equal("error", sut.StatusClass);
        Assert.Contains("Disconnect error", sut.StatusMessage);
    }

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
    public async Task OnTimeularChange_WhenTargetAlreadyActive_LogsAlreadyActive()
    {
        var settings = new StubSettingsService();
        var timeTracking = await CreateLoadedTimeTrackingServiceAsync(settings);
        var firstActivity = timeTracking.Account.Activities.OrderBy(m => m.DisplayOrder).First();
        timeTracking.ActivateActivity(firstActivity.Id, "Work");

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
            Face = 1,
            RawHex = "0x01"
        });

        Assert.Contains("already active", sut.ChangeLog[0].Message);
    }

    [Fact]
    public async Task OnTimeularChange_BatteryEvent_UpdatesBatteryLevel()
    {
        var settings = new StubSettingsService();
        var timeTracking = await CreateLoadedTimeTrackingServiceAsync(settings);
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
            EventType = "battery",
            BatteryLevel = 75
        });

        Assert.Equal(75, sut.BatteryLevel);
    }

    [Fact]
    public async Task OnTimeularChange_GenericEvent_LogsEvent()
    {
        var settings = new StubSettingsService();
        var timeTracking = await CreateLoadedTimeTrackingServiceAsync(settings);
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
            EventType = "other_event",
            TimestampUtc = DateTimeOffset.UtcNow.ToString("O")
        });

        Assert.Contains("Received a device event", sut.ChangeLog[0].Message);
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

    [Fact]
    public async Task Dispose_UnregistersListener()
    {
        var js = new TestJsRuntime();
        var settings = new StubSettingsService();
        var timeTracking = await CreateLoadedTimeTrackingServiceAsync(settings);
        var sut = new TimeularService(
            js,
            timeTracking,
            new ActivityStartPromptService(timeTracking, new StubStringLocalizer()),
            new StubNotificationService(),
            settings,
            new StubStringLocalizer(),
            NullLogger<TimeularService>.Instance);

        await sut.InitializeAsync();
        sut.Dispose();

        Assert.Contains(js.Invocations, x => x.Identifier == "timeularInterop.unregisterListener");
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
