namespace Kairos.Shared.Models;

/// <summary>
/// Represents the user's time tracking account state.
/// </summary>
public class TimeAccount
{
    /// <summary>
    /// All activity events (historical and current).
    /// </summary>
    public List<ActivityEvent> Events { get; set; } = new();

    /// <summary>
    /// Configured activities available to the user.
    /// </summary>
    public List<Activity> Activities { get; set; } = new();

    /// <summary>
    /// The selected timeline period for the chart.
    /// </summary>
    public TimeSpan TimelinePeriod { get; set; } = TimeSpan.FromHours(24);
}
