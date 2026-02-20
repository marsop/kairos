namespace Budgetr.Shared.Models;

/// <summary>
/// Represents a configurable meter that can be activated to track time.
/// </summary>
public class Meter
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Display name for the meter (e.g., "+1x", "-1x", "+1.5x").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The multiplier for this meter. Positive values add time, negative subtract.
    /// </summary>
    private double _factor;

    /// <summary>
    /// The multiplier for this meter. Positive values add time, negative subtract.
    /// Must be between -10 and 10.
    /// </summary>
    public double Factor
    {
        get => _factor;
        set
        {
            if (value < -10 || value > 10)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Factor must be between -10 and 10.");
            }
            _factor = value;
        }
    }

    /// <summary>
    /// Order in which this meter appears in the UI.
    /// </summary>
    public int DisplayOrder { get; set; }
}
