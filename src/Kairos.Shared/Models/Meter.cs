namespace Kairos.Shared.Models;

/// <summary>
/// Represents a configurable meter that can be activated to track time.
/// </summary>
public class Meter
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Display name for the meter.
    /// </summary>
    public string Name { get; set; } = string.Empty;

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
    /// Order in which this meter appears in the UI.
    /// </summary>
    public int DisplayOrder { get; set; }
}
