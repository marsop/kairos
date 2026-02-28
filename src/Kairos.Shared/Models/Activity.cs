namespace Kairos.Shared.Models;

/// <summary>
/// Represents a configurable activity that can be activated to track time.
/// </summary>
public class Activity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Display name for the activity.
    /// </summary>
    public string Name { get; set; } = string.Empty;

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
    /// Order in which this activity appears in the UI.
    /// </summary>
    public int DisplayOrder { get; set; }
}
