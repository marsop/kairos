namespace Budgetr.Shared.Models;

/// <summary>
/// Represents the user's time tracking account state.
/// </summary>
public class TimeAccount
{
    /// <summary>
    /// All meter events (historical and current).
    /// </summary>
    public List<MeterEvent> Events { get; set; } = new();

    /// <summary>
    /// Configured meters available to the user.
    /// </summary>
    public List<Meter> Meters { get; set; } = new();

    /// <summary>
    /// The selected timeline period for the chart.
    /// </summary>
    public TimeSpan TimelinePeriod { get; set; } = TimeSpan.FromHours(24);
}
