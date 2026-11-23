using Kairos.Core.Models;
using Kairos.Application.Services;

namespace Kairos.ValidationTest;

public class ActivityStartPromptServiceTests
{
    [Fact]
    public async Task TryConfirm_ValidComment_ActivatesPendingActivity()
    {
        var timeTracking = await CreateLoadedTimeTrackingServiceAsync();
        var activity = timeTracking.Account.Activities.First();
        var sut = new ActivityStartPromptService(timeTracking, new StubStringLocalizer());

        sut.RequestStart(activity.Id);

        var result = sut.TryConfirm("Focus block", out var errorMessage);

        Assert.True(result);
        Assert.Null(errorMessage);
        Assert.Null(sut.PendingActivityId);
        Assert.Equal(activity.Name, timeTracking.GetActiveEvent()!.ActivityName);
        Assert.Equal("Focus block", timeTracking.GetActiveEvent()!.Comment);
    }

    [Fact]
    public async Task TryConfirm_EmptyComment_ReturnsLocalizedValidationError()
    {
        var timeTracking = await CreateLoadedTimeTrackingServiceAsync();
        var activity = timeTracking.Account.Activities.First();
        var sut = new ActivityStartPromptService(timeTracking, new StubStringLocalizer());

        sut.RequestStart(activity.Id);

        var result = sut.TryConfirm(string.Empty, out var errorMessage);

        Assert.False(result);
        Assert.Equal("CommentLengthError", errorMessage);
        Assert.Equal(activity.Id, sut.PendingActivityId);
        Assert.Null(timeTracking.GetActiveEvent());
    }

    [Fact]
    public async Task ConsumeRecentConfirmation_ReturnsTrueOnceForRecentlyConfirmedActivity()
    {
        var timeTracking = await CreateLoadedTimeTrackingServiceAsync();
        var activity = timeTracking.Account.Activities.First();
        var sut = new ActivityStartPromptService(timeTracking, new StubStringLocalizer());

        sut.RequestStart(activity.Id);
        var confirmed = sut.TryConfirm("Focus block", out _);

        Assert.True(confirmed);
        Assert.True(sut.ConsumeRecentConfirmation(activity.Id, TimeSpan.FromSeconds(1)));
        Assert.False(sut.ConsumeRecentConfirmation(activity.Id, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task Cancel_ClearsPendingActivityIdAndNotifies()
    {
        var timeTracking = await CreateLoadedTimeTrackingServiceAsync();
        var activity = timeTracking.Account.Activities.First();
        var sut = new ActivityStartPromptService(timeTracking, new StubStringLocalizer());

        var eventCount = 0;
        sut.OnStateChanged += () => eventCount++;

        // When null, Cancel does nothing
        sut.Cancel();
        Assert.Equal(0, eventCount);

        // When pending, Cancel clears
        sut.RequestStart(activity.Id);
        Assert.Equal(activity.Id, sut.PendingActivityId);
        Assert.Equal(1, eventCount);

        sut.Cancel();
        Assert.Null(sut.PendingActivityId);
        Assert.Equal(2, eventCount);
    }

    [Fact]
    public async Task TryConfirm_WhenNoPendingActivity_ReturnsFalse()
    {
        var timeTracking = await CreateLoadedTimeTrackingServiceAsync();
        var sut = new ActivityStartPromptService(timeTracking, new StubStringLocalizer());

        var result = sut.TryConfirm("Comment", out var error);

        Assert.False(result);
        Assert.Null(error);
    }

    [Fact]
    public async Task TryConfirm_WhenCommentTooLong_ReturnsError()
    {
        var timeTracking = await CreateLoadedTimeTrackingServiceAsync();
        var activity = timeTracking.Account.Activities.First();
        var sut = new ActivityStartPromptService(timeTracking, new StubStringLocalizer());

        sut.RequestStart(activity.Id);
        var longComment = new string('x', 251);
        var result = sut.TryConfirm(longComment, out var error);

        Assert.False(result);
        Assert.Equal("CommentLengthError", error);
    }

    [Fact]
    public async Task ConsumeRecentConfirmation_WhenExpiredOrDifferentActivity_ReturnsFalse()
    {
        var timeTracking = await CreateLoadedTimeTrackingServiceAsync();
        var activity = timeTracking.Account.Activities.First();
        var sut = new ActivityStartPromptService(timeTracking, new StubStringLocalizer());

        sut.RequestStart(activity.Id);
        sut.TryConfirm("Focus", out _);

        // Different activity
        Assert.False(sut.ConsumeRecentConfirmation(Guid.NewGuid(), TimeSpan.FromSeconds(10)));

        // Re-confirm and test expired
        sut.RequestStart(activity.Id);
        sut.TryConfirm("Focus", out _);
        Assert.False(sut.ConsumeRecentConfirmation(activity.Id, TimeSpan.FromMilliseconds(-1)));
    }

    private static async Task<TimeTrackingService> CreateLoadedTimeTrackingServiceAsync()
    {
        var storage = new InMemoryStorageService();
        var config = new StubActivityConfigurationService(new[]
        {
            new Activity { Name = "Work", Color = "#10B981", DisplayOrder = 0 },
            new Activity { Name = "Break", Color = "#EF4444", DisplayOrder = 1 }
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
