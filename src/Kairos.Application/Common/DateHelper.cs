namespace Kairos.Application.Common;

/// <summary>
/// Helper methods for date calculations, such as determining week boundaries.
/// </summary>
public static class DateHelper
{
    /// <summary>
    /// Returns the Monday representing the start of the week for the specified date.
    /// </summary>
    public static DateOnly GetStartOfWeek(DateOnly date)
    {
        int diff = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-diff);
    }

    /// <summary>
    /// Returns the Monday representing the start of the week for the specified DateTime, with time component cleared to midnight.
    /// </summary>
    public static DateTime GetStartOfWeek(DateTime date)
    {
        int diff = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-diff).Date;
    }

    /// <summary>
    /// Returns the Monday representing the start of the week for the specified DateTimeOffset, with time component cleared to UTC midnight.
    /// </summary>
    public static DateTimeOffset GetStartOfWeek(DateTimeOffset date)
    {
        int diff = ((int)date.DayOfWeek + 6) % 7;
        return new DateTimeOffset(date.AddDays(-diff).Date, TimeSpan.Zero);
    }
}
