namespace Budgetr.Shared.Models;

/// <summary>
/// Represents a single meter activation event.
/// When a meter is active, time is added or subtracted from the account
/// based on the Factor multiplier.
/// </summary>
public class MeterEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// When the meter was activated.
    /// </summary>
    public DateTimeOffset StartTime { get; set; }
    
    /// <summary>
    /// When the meter was deactivated. Null if still active.
    /// </summary>
    public DateTimeOffset? EndTime { get; set; }
    
    /// <summary>
    /// The multiplier for this meter (e.g., 1.0, -1.0, 1.5, -2.0).
    /// Positive values add time, negative values subtract.
    /// </summary>
    public double Factor { get; set; }
    
    /// <summary>
    /// Display name for the meter (e.g., "+1x", "-1x", "+1.5x").
    /// </summary>
    public string MeterName { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether this event is currently active (meter still running).
    /// </summary>
    public bool IsActive => EndTime == null;
    
    /// <summary>
    /// Calculates the duration this meter has been/was active.
    /// </summary>
    public TimeSpan Duration => IsActive 
        ? DateTimeOffset.UtcNow - StartTime 
        : (EndTime!.Value - StartTime);
    
    /// <summary>
    /// Calculates the time contribution of this event (duration * factor).
    /// </summary>
    public TimeSpan TimeContribution => TimeSpan.FromTicks((long)(Duration.Ticks * Factor));
}
