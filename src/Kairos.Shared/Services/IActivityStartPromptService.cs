namespace Kairos.Shared.Services;

/// <summary>
/// Coordinates the shared activity start comment prompt across the app.
/// </summary>
public interface IActivityStartPromptService
{
    Guid? PendingActivityId { get; }

    event Action? OnStateChanged;

    void RequestStart(Guid activityId);

    void Cancel();

    bool TryConfirm(string comment, out string? errorMessage);
}
