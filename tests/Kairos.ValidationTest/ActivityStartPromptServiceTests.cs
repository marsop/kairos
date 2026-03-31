using Kairos.Shared.Models;
using Kairos.Shared.Services;

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

    private static async Task<TimeTrackingService> CreateLoadedTimeTrackingServiceAsync()
    {
        var storage = new InMemoryStorageService();
        var config = new StubActivityConfigurationService(new[]
        {
            new Activity { Name = "Work", Color = "#10B981", Factor = 1, DisplayOrder = 0 },
            new Activity { Name = "Break", Color = "#EF4444", Factor = 1, DisplayOrder = 1 }
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
