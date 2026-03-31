using Kairos.Shared.Resources;
using Microsoft.Extensions.Localization;

namespace Kairos.Shared.Services;

/// <summary>
/// Centralizes the "start activity with comment" flow so manual and device-driven starts share the same prompt.
/// </summary>
public sealed class ActivityStartPromptService : IActivityStartPromptService
{
    private readonly ITimeTrackingService _timeService;
    private readonly IStringLocalizer<Strings> _localizer;
    private Guid? _recentlyConfirmedActivityId;
    private DateTimeOffset? _recentConfirmationAtUtc;

    public Guid? PendingActivityId { get; private set; }

    public event Action? OnStateChanged;

    public ActivityStartPromptService(ITimeTrackingService timeService, IStringLocalizer<Strings> localizer)
    {
        _timeService = timeService;
        _localizer = localizer;
    }

    public void RequestStart(Guid activityId)
    {
        PendingActivityId = activityId;
        OnStateChanged?.Invoke();
    }

    public void Cancel()
    {
        if (!PendingActivityId.HasValue)
        {
            return;
        }

        PendingActivityId = null;
        OnStateChanged?.Invoke();
    }

    public bool TryConfirm(string comment, out string? errorMessage)
    {
        errorMessage = null;

        if (!PendingActivityId.HasValue)
        {
            return false;
        }

        var trimmedComment = comment?.Trim() ?? string.Empty;
        if (trimmedComment.Length < TimeTrackingService.MinCommentLength || trimmedComment.Length > TimeTrackingService.MaxCommentLength)
        {
            errorMessage = _localizer["CommentLengthError"];
            return false;
        }

        try
        {
            _timeService.ActivateActivity(PendingActivityId.Value, trimmedComment);
            _recentlyConfirmedActivityId = PendingActivityId.Value;
            _recentConfirmationAtUtc = DateTimeOffset.UtcNow;
            PendingActivityId = null;
            OnStateChanged?.Invoke();
            return true;
        }
        catch (ArgumentException ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public bool ConsumeRecentConfirmation(Guid activityId, TimeSpan maxAge)
    {
        if (_recentlyConfirmedActivityId != activityId || !_recentConfirmationAtUtc.HasValue)
        {
            return false;
        }

        var age = DateTimeOffset.UtcNow - _recentConfirmationAtUtc.Value;
        _recentlyConfirmedActivityId = null;
        _recentConfirmationAtUtc = null;
        return age <= maxAge;
    }
}
