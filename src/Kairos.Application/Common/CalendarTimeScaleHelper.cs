namespace Kairos.Application.Common;

/// <summary>
/// Represents a time marker on the calendar's time axis and grid.
/// </summary>
public record CalendarTimeMarker(
    int Seconds,
    string Label,
    bool IsMajorHour
);

/// <summary>
/// Helper for calculating calendar time axis markers and intervals based on zoom level.
/// </summary>
public static class CalendarTimeScaleHelper
{
    private static readonly int[] CandidateIntervalsMinutes = [1, 2, 5, 10, 15, 30, 60, 120];

    /// <summary>
    /// Computes the optimal time marker interval in minutes based on pixels per hour and viewport height.
    /// Follows a heuristic that aims to display at least minLabelCount readable labels across the viewport
    /// without violating minimum pixel spacing.
    /// </summary>
    public static int GetTimeIntervalMinutes(
        double pixelsPerHour,
        double viewportHeight,
        int minLabelCount = 6,
        double minSpacingPx = 28.0)
    {
        if (viewportHeight <= 0) viewportHeight = 600.0;
        if (pixelsPerHour <= 0) pixelsPerHour = 60.0;
        if (minLabelCount <= 0) minLabelCount = 6;
        if (minSpacingPx <= 0) minSpacingPx = 28.0;

        // Maximum pixel distance between consecutive labels to guarantee at least minLabelCount visible labels
        double maxSpacingPx = viewportHeight / minLabelCount;

        // Eligible candidates where spacing is at least minSpacingPx and does not exceed maxSpacingPx
        var candidatesWithMinCount = CandidateIntervalsMinutes
            .Where(m =>
            {
                double spacing = (m / 60.0) * pixelsPerHour;
                return spacing >= minSpacingPx && spacing <= maxSpacingPx;
            })
            .ToList();

        if (candidatesWithMinCount.Count > 0)
        {
            // Pick the largest interval that still provides >= minLabelCount labels
            return candidatesWithMinCount.Max();
        }

        // If no candidate satisfies both, find candidates that do not collide (>= minSpacingPx)
        var nonCollidingCandidates = CandidateIntervalsMinutes
            .Where(m => (m / 60.0) * pixelsPerHour >= minSpacingPx)
            .ToList();

        if (nonCollidingCandidates.Count > 0)
        {
            // Pick the smallest interval that does not collide (provides the highest label count possible)
            return nonCollidingCandidates.Min();
        }

        // If even the largest candidate has spacing < minSpacingPx (e.g. extremely small pixelsPerHour),
        // return the largest candidate to keep labels as sparse as possible.
        return CandidateIntervalsMinutes[^1];
    }

    /// <summary>
    /// Generates time markers for the full 24-hour day (00:00 to 23:59) at the optimal interval.
    /// </summary>
    public static IReadOnlyList<CalendarTimeMarker> GetTimeMarkers(
        double pixelsPerHour,
        double viewportHeight,
        int minLabelCount = 6,
        double minSpacingPx = 28.0)
    {
        int intervalMinutes = GetTimeIntervalMinutes(pixelsPerHour, viewportHeight, minLabelCount, minSpacingPx);
        var markers = new List<CalendarTimeMarker>();

        for (int totalMinutes = 0; totalMinutes < 24 * 60; totalMinutes += intervalMinutes)
        {
            int hours = totalMinutes / 60;
            int minutes = totalMinutes % 60;
            int seconds = totalMinutes * 60;
            string label = $"{hours:D2}:{minutes:D2}";
            bool isMajorHour = (minutes == 0);

            markers.Add(new CalendarTimeMarker(seconds, label, isMajorHour));
        }

        return markers;
    }
}
