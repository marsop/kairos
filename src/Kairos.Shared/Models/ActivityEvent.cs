namespace Kairos.Shared.Models;

/// <summary>
/// Represents a single activity activation event.
/// When a activity is active, time is added to the account
/// based on a fixed 1.0 factor.
/// </summary>
public class ActivityEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// When the activity was activated.
    /// </summary>
    public DateTimeOffset StartTime { get; set; }
    
    /// <summary>
    /// When the activity was deactivated. Null if still active.
    /// </summary>
    public DateTimeOffset? EndTime { get; set; }
    
    /// <summary>
    /// Activity factor is fixed at 1.0.
    /// </summary>
    private double _factor = 1.0;

    /// <summary>
    /// Activity factor is fixed at 1.0.
    /// </summary>
    public double Factor
    {
        get => _factor;
        set => _factor = 1.0;
    }
    
    /// <summary>
    /// Display name for the activity.
    /// </summary>
    public string ActivityName { get; set; } = string.Empty;

    /// <summary>
    /// User comment captured when this activity event started.
    /// </summary>
    public string Comment { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether this event is currently active (activity still running).
    /// </summary>
    public bool IsActive => EndTime == null;
    
    /// <summary>
    /// Calculates the duration this activity has been/was active.
    /// </summary>
    public TimeSpan Duration => IsActive 
        ? DateTimeOffset.UtcNow - StartTime 
        : (EndTime!.Value - StartTime);
    
    /// <summary>
    /// Calculates the time contribution of this event (duration * 1.0).
    /// </summary>
    public TimeSpan TimeContribution => TimeSpan.FromTicks((long)(Duration.Ticks * Factor));
}
