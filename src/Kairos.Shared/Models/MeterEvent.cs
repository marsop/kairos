namespace Kairos.Shared.Models;

/// <summary>
/// Represents a single meter activation event.
/// When a meter is active, time is added to the account
/// based on a fixed 1.0 factor.
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
    /// Meter factor is fixed at 1.0.
    /// </summary>
    private double _factor = 1.0;

    /// <summary>
    /// Meter factor is fixed at 1.0.
    /// </summary>
    public double Factor
    {
        get => _factor;
        set => _factor = 1.0;
    }
    
    /// <summary>
    /// Display name for the meter.
    /// </summary>
    public string MeterName { get; set; } = string.Empty;

    /// <summary>
    /// User comment captured when this meter event started.
    /// </summary>
    public string Comment { get; set; } = string.Empty;
    
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
    /// Calculates the time contribution of this event (duration * 1.0).
    /// </summary>
    public TimeSpan TimeContribution => TimeSpan.FromTicks((long)(Duration.Ticks * Factor));
}
