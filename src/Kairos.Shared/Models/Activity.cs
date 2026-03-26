namespace Kairos.Shared.Models;

/// <summary>
/// Represents a configurable activity that can be activated to track time.
/// </summary>
public class Activity
{
    public const string DefaultColor = "#10B981";

    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Display name for the activity.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Hex color used to identify the activity in the UI.
    /// </summary>
    public string Color { get; set; } = DefaultColor;

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

    public static string NormalizeColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            throw new ArgumentException("Activity color must be a valid hex color in the form #RRGGBB.");
        }

        var trimmed = color.Trim();
        if (trimmed.Length != 7 || trimmed[0] != '#')
        {
            throw new ArgumentException("Activity color must be a valid hex color in the form #RRGGBB.");
        }

        return string.Create(7, trimmed, static (span, source) =>
        {
            span[0] = '#';

            for (var i = 1; i < source.Length; i++)
            {
                var ch = source[i];
                if (!Uri.IsHexDigit(ch))
                {
                    throw new ArgumentException("Activity color must be a valid hex color in the form #RRGGBB.");
                }

                span[i] = char.ToUpperInvariant(ch);
            }
        });
    }

    public static string SanitizeColor(string? color)
    {
        try
        {
            return NormalizeColor(color);
        }
        catch (ArgumentException)
        {
            return DefaultColor;
        }
    }
}
