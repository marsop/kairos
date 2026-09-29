namespace Kairos.Application.Common;

using Kairos.Core.Models;

/// <summary>
/// Represents an untracked gap between activities.
/// </summary>
public record ScheduleGap(
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    TimeSpan Duration
)
{
    public double GetStartSeconds(DateOnly date)
    {
        var localStart = StartTime.ToLocalTime();
        if (DateOnly.FromDateTime(localStart.Date) < date)
        {
            return 0;
        }
        return Math.Ceiling(localStart.TimeOfDay.TotalSeconds);
    }

    public double GetEndSeconds(DateOnly date)
    {
        var localEnd = EndTime.ToLocalTime();
        if (DateOnly.FromDateTime(localEnd.Date) > date)
        {
            return 86400;
        }
        return Math.Floor(localEnd.TimeOfDay.TotalSeconds);
    }
}

/// <summary>
/// Represents overlap information for a specific event.
/// </summary>
public record EventOverlapInfo(
    Guid EventId,
    IReadOnlyList<Guid> OverlappingEventIds,
    IReadOnlyList<string> OverlappingActivityNames
);

/// <summary>
/// Analysis result containing detected gaps and overlapping events.
/// </summary>
public record ScheduleAnalysisResult(
    IReadOnlyList<ScheduleGap> Gaps,
    IReadOnlyDictionary<Guid, EventOverlapInfo> Overlaps,
    IReadOnlyDictionary<Guid, ScheduleGap> GapsBeforeEvent
);

/// <summary>
/// Helper to detect schedule overlaps and untracked gaps.
/// </summary>
public static class ScheduleAnalysisHelper
{
    public static readonly TimeSpan MinGapThreshold = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan MaxGapThreshold = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Analyzes the given events for overlapping time slots and untracked gaps between 1 and 10 minutes.
    /// </summary>
    public static ScheduleAnalysisResult Analyze(
        IEnumerable<ActivityEvent> events,
        DateTimeOffset? now = null,
        DateOnly? selectedDate = null)
    {
        var eventList = events.ToList();
        var effectiveNow = now ?? DateTimeOffset.Now;

        // 1. Calculate Overlaps
        var overlaps = new Dictionary<Guid, (List<Guid> Ids, HashSet<string> Names)>();
        for (int i = 0; i < eventList.Count; i++)
        {
            var e1 = eventList[i];
            var start1 = e1.StartTime;
            var end1 = e1.EndTime ?? effectiveNow;
            if (end1 < start1) end1 = start1;

            for (int j = i + 1; j < eventList.Count; j++)
            {
                var e2 = eventList[j];
                var start2 = e2.StartTime;
                var end2 = e2.EndTime ?? effectiveNow;
                if (end2 < start2) end2 = start2;

                // Two events overlap if start1 < end2 and start2 < end1
                if (start1 < end2 && start2 < end1)
                {
                    if (!overlaps.ContainsKey(e1.Id))
                    {
                        overlaps[e1.Id] = (new List<Guid>(), new HashSet<string>());
                    }
                    overlaps[e1.Id].Ids.Add(e2.Id);
                    if (!string.IsNullOrWhiteSpace(e2.ActivityName))
                    {
                        overlaps[e1.Id].Names.Add(e2.ActivityName);
                    }

                    if (!overlaps.ContainsKey(e2.Id))
                    {
                        overlaps[e2.Id] = (new List<Guid>(), new HashSet<string>());
                    }
                    overlaps[e2.Id].Ids.Add(e1.Id);
                    if (!string.IsNullOrWhiteSpace(e1.ActivityName))
                    {
                        overlaps[e2.Id].Names.Add(e1.ActivityName);
                    }
                }
            }
        }

        var overlapResult = overlaps.ToDictionary(
            kvp => kvp.Key,
            kvp => new EventOverlapInfo(kvp.Key, kvp.Value.Ids, kvp.Value.Names.ToList())
        );

        // 2. Calculate Gaps between events (chronological order)
        var gaps = new List<ScheduleGap>();
        var gapsBefore = new Dictionary<Guid, ScheduleGap>();

        if (eventList.Count >= 2)
        {
            var sortedEvents = eventList.OrderBy(e => e.StartTime).ToList();
            var currentCoverageEnd = sortedEvents[0].EndTime ?? effectiveNow;
            if (currentCoverageEnd < sortedEvents[0].StartTime)
            {
                currentCoverageEnd = sortedEvents[0].StartTime;
            }

            for (int i = 1; i < sortedEvents.Count; i++)
            {
                var evt = sortedEvents[i];
                var evtStart = evt.StartTime;
                var evtEnd = evt.EndTime ?? effectiveNow;
                if (evtEnd < evtStart)
                {
                    evtEnd = evtStart;
                }

                if (evtStart > currentCoverageEnd)
                {
                    var gapDuration = evtStart - currentCoverageEnd;
                    if (gapDuration >= MinGapThreshold && gapDuration <= MaxGapThreshold)
                    {
                        var gap = new ScheduleGap(currentCoverageEnd, evtStart, gapDuration);
                        gaps.Add(gap);
                        gapsBefore[evt.Id] = gap;
                    }
                }

                if (evtEnd > currentCoverageEnd)
                {
                    currentCoverageEnd = evtEnd;
                }
            }
        }

        return new ScheduleAnalysisResult(gaps, overlapResult, gapsBefore);
    }
}
