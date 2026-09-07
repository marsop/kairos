using Kairos.Application.Services;
using Kairos.Core.Models;

namespace Kairos.ValidationTest;

public class TimeTrackingServiceResumeTests
{
    private static async Task<TimeTrackingService> CreateLoadedServiceAsync(
        StubSettingsService? settingsService = null,
        StubNotificationService? notificationService = null)
    {
        var storage = new InMemoryStorageService();
        var config = new StubActivityConfigurationService(new[]
        {
            new Activity { Id = Guid.NewGuid(), Name = "Work", Color = "#10B981", DisplayOrder = 0, ActivityGroupId = 0 },
            new Activity { Id = Guid.NewGuid(), Name = "Personal", Color = "#3B82F6", DisplayOrder = 1, ActivityGroupId = 1 },
            new Activity { Id = Guid.NewGuid(), Name = "Break", Color = "#EF4444", DisplayOrder = 2, ActivityGroupId = 0 }
        });
        var settings = settingsService ?? new StubSettingsService();
        var notifications = notificationService ?? new StubNotificationService();
        var localizer = new StubStringLocalizer();
        var service = new TimeTrackingService(
            storage,
            config,
            settings,
            notifications,
            localizer,
            new StubSupabaseAuthService(),
            new StubSupabaseActivityStore());
        await service.LoadAsync();
        return service;
    }

    [Fact]
    public async Task GetLastCompletedEventToday_NoEvents_ReturnsNull()
    {
        var sut = await CreateLoadedServiceAsync();

        var result = sut.GetLastCompletedEventToday();

        Assert.Null(result);
    }

    [Fact]
    public async Task GetLastCompletedEventToday_EventEndedYesterday_ReturnsNull()
    {
        var sut = await CreateLoadedServiceAsync();
        var workActivity = sut.Account.Activities.First(a => a.Name == "Work");

        sut.Account.Events.Add(new ActivityEvent
        {
            Id = Guid.NewGuid(),
            ActivityId = workActivity.Id,
            ActivityName = workActivity.Name,
            StartTime = DateTimeOffset.Now.AddDays(-1).AddHours(-2),
            EndTime = DateTimeOffset.Now.AddDays(-1).AddHours(-1),
            Comment = "Yesterday work"
        });

        var result = sut.GetLastCompletedEventToday();

        Assert.Null(result);
    }

    [Fact]
    public async Task GetLastCompletedEventToday_ActivityDeleted_ReturnsNull()
    {
        var sut = await CreateLoadedServiceAsync();

        sut.Account.Events.Add(new ActivityEvent
        {
            Id = Guid.NewGuid(),
            ActivityId = Guid.NewGuid(), // Not in Activities
            ActivityName = "Old Deleted Activity",
            StartTime = DateTimeOffset.Now.AddHours(-2),
            EndTime = DateTimeOffset.Now.AddHours(-1),
            Comment = "Old comment"
        });

        var result = sut.GetLastCompletedEventToday();

        Assert.Null(result);
    }

    [Fact]
    public async Task GetLastCompletedEventToday_WithActivityGroupFilter_MatchesGroup()
    {
        var sut = await CreateLoadedServiceAsync();
        var workActivity = sut.Account.Activities.First(a => a.Name == "Work"); // Group 0
        var personalActivity = sut.Account.Activities.First(a => a.Name == "Personal"); // Group 1

        // Event 1: Work (Group 0) finished 2 hours ago
        sut.Account.Events.Add(new ActivityEvent
        {
            Id = Guid.NewGuid(),
            ActivityId = workActivity.Id,
            ActivityName = workActivity.Name,
            StartTime = DateTimeOffset.Now.AddHours(-3),
            EndTime = DateTimeOffset.Now.AddHours(-2),
            Comment = "Work task"
        });

        // Event 2: Personal (Group 1) finished 1 hour ago
        sut.Account.Events.Add(new ActivityEvent
        {
            Id = Guid.NewGuid(),
            ActivityId = personalActivity.Id,
            ActivityName = personalActivity.Name,
            StartTime = DateTimeOffset.Now.AddHours(-2),
            EndTime = DateTimeOffset.Now.AddHours(-1),
            Comment = "Personal task"
        });

        // Querying for Group 0 should return Work
        var group0Result = sut.GetLastCompletedEventToday(activityGroupId: 0);
        Assert.NotNull(group0Result);
        Assert.Equal(workActivity.Id, group0Result.ActivityId);

        // Querying for Group 1 should return Personal
        var group1Result = sut.GetLastCompletedEventToday(activityGroupId: 1);
        Assert.NotNull(group1Result);
        Assert.Equal(personalActivity.Id, group1Result.ActivityId);

        // Querying with no group filter should return Personal (latest completed)
        var noFilterResult = sut.GetLastCompletedEventToday(activityGroupId: null);
        Assert.NotNull(noFilterResult);
        Assert.Equal(personalActivity.Id, noFilterResult.ActivityId);
    }

    [Fact]
    public async Task ResumeEvent_ValidCompletedEvent_MovesToActiveStateAndNotifies()
    {
        var notifications = new StubNotificationService();
        var sut = await CreateLoadedServiceAsync(notificationService: notifications);
        var workActivity = sut.Account.Activities.First(a => a.Name == "Work");

        var startTime = DateTimeOffset.Now.AddHours(-1);
        var endTime = DateTimeOffset.Now.AddMinutes(-10);
        var eventId = Guid.NewGuid();

        var completedEvent = new ActivityEvent
        {
            Id = eventId,
            ActivityId = workActivity.Id,
            ActivityName = workActivity.Name,
            StartTime = startTime,
            EndTime = endTime,
            Comment = "Resumable work"
        };
        sut.Account.Events.Add(completedEvent);

        bool stateChangedFired = false;
        sut.OnStateChanged += () => stateChangedFired = true;

        sut.ResumeEvent(eventId);

        Assert.True(completedEvent.IsActive);
        Assert.Null(completedEvent.EndTime);
        Assert.Equal(startTime, completedEvent.StartTime);
        Assert.Same(completedEvent, sut.GetActiveEvent());
        Assert.True(stateChangedFired);
        Assert.NotEmpty(notifications.SentNotifications);
    }

    [Fact]
    public async Task ResumeEvent_WhileAnotherEventActive_ThrowsInvalidOperationException()
    {
        var sut = await CreateLoadedServiceAsync();
        var workActivity = sut.Account.Activities.First(a => a.Name == "Work");
        var breakActivity = sut.Account.Activities.First(a => a.Name == "Break");

        var completedEvent = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            ActivityId = workActivity.Id,
            ActivityName = workActivity.Name,
            StartTime = DateTimeOffset.Now.AddHours(-2),
            EndTime = DateTimeOffset.Now.AddHours(-1),
            Comment = "Done work"
        };
        sut.Account.Events.Add(completedEvent);

        // Start another activity
        sut.ActivateActivity(breakActivity.Id, "Currently breaking");

        Assert.Throws<InvalidOperationException>(() => sut.ResumeEvent(completedEvent.Id));
    }
}
