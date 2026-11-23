namespace Kairos.Application.Common;

using System;
using System.Collections.Generic;
using System.Linq;
using Kairos.Core.Models;

/// <summary>
/// Helper to handle magnetic snapping of calendar events to adjacent event boundaries based on StickyEventsDuration.
/// </summary>
public static class CalendarMagneticSnappingHelper
{
    /// <summary>
    /// Finds the closest boundary (start or end) among reference events within the given threshold.
    /// Returns the exact candidate time if no boundary is within threshold.
    /// </summary>
    public static DateTimeOffset SnapPoint(
        DateTimeOffset candidateTime,
        IEnumerable<ActivityEvent>? referenceEvents,
        TimeSpan threshold,
        Guid? excludedEventId = null)
    {
        if (threshold <= TimeSpan.Zero || referenceEvents == null)
        {
            return candidateTime;
        }

        DateTimeOffset? closestBoundary = null;
        var minDiff = TimeSpan.MaxValue;

        foreach (var evt in referenceEvents)
        {
            if (evt == null || (excludedEventId.HasValue && evt.Id == excludedEventId.Value))
            {
                continue;
            }

            // Check StartTime boundary
            var diffStart = (evt.StartTime - candidateTime).Duration();
            if (diffStart <= threshold && diffStart < minDiff)
            {
                minDiff = diffStart;
                closestBoundary = evt.StartTime;
            }

            // Check EndTime boundary (if set)
            if (evt.EndTime.HasValue)
            {
                var diffEnd = (evt.EndTime.Value - candidateTime).Duration();
                if (diffEnd <= threshold && diffEnd < minDiff)
                {
                    minDiff = diffEnd;
                    closestBoundary = evt.EndTime.Value;
                }
            }
        }

        return closestBoundary ?? candidateTime;
    }

    /// <summary>
    /// Snaps candidate start time when resizing an event's start boundary, ensuring start stays before current end.
    /// </summary>
    public static DateTimeOffset SnapResizeStart(
        DateTimeOffset candidateStart,
        DateTimeOffset currentEnd,
        IEnumerable<ActivityEvent>? referenceEvents,
        TimeSpan threshold,
        Guid? excludedEventId = null)
    {
        var snapped = SnapPoint(candidateStart, referenceEvents, threshold, excludedEventId);
        return snapped < currentEnd ? snapped : candidateStart;
    }

    /// <summary>
    /// Snaps candidate end time when resizing an event's end boundary, ensuring end stays after current start and does not exceed maxAllowedEnd.
    /// </summary>
    public static DateTimeOffset SnapResizeEnd(
        DateTimeOffset currentStart,
        DateTimeOffset candidateEnd,
        IEnumerable<ActivityEvent>? referenceEvents,
        TimeSpan threshold,
        DateTimeOffset maxAllowedEnd,
        Guid? excludedEventId = null)
    {
        var snapped = SnapPoint(candidateEnd, referenceEvents, threshold, excludedEventId);
        return snapped > currentStart && snapped <= maxAllowedEnd ? snapped : candidateEnd;
    }

    /// <summary>
    /// Snaps candidate start and end times when moving an event, preserving duration and choosing the closest boundary.
    /// </summary>
    public static (DateTimeOffset Start, DateTimeOffset End) SnapMove(
        DateTimeOffset rawStart,
        DateTimeOffset rawEnd,
        IEnumerable<ActivityEvent>? referenceEvents,
        TimeSpan threshold,
        DateTimeOffset maxAllowedEnd,
        Guid? excludedEventId = null)
    {
        if (threshold <= TimeSpan.Zero || referenceEvents == null)
        {
            return (rawStart, rawEnd);
        }

        var duration = rawEnd - rawStart;
        if (duration <= TimeSpan.Zero)
        {
            return (rawStart, rawEnd);
        }

        DateTimeOffset? bestStartSnap = null;
        var minStartDiff = TimeSpan.MaxValue;

        DateTimeOffset? bestEndSnap = null;
        var minEndDiff = TimeSpan.MaxValue;

        foreach (var evt in referenceEvents)
        {
            if (evt == null || (excludedEventId.HasValue && evt.Id == excludedEventId.Value))
            {
                continue;
            }

            // Check boundaries against rawStart
            var diffStartToStart = (evt.StartTime - rawStart).Duration();
            if (diffStartToStart <= threshold && diffStartToStart < minStartDiff)
            {
                minStartDiff = diffStartToStart;
                bestStartSnap = evt.StartTime;
            }

            if (evt.EndTime.HasValue)
            {
                var diffStartToEnd = (evt.EndTime.Value - rawStart).Duration();
                if (diffStartToEnd <= threshold && diffStartToEnd < minStartDiff)
                {
                    minStartDiff = diffStartToEnd;
                    bestStartSnap = evt.EndTime.Value;
                }
            }

            // Check boundaries against rawEnd
            var diffEndToStart = (evt.StartTime - rawEnd).Duration();
            if (diffEndToStart <= threshold && diffEndToStart < minEndDiff)
            {
                minEndDiff = diffEndToStart;
                bestEndSnap = evt.StartTime;
            }

            if (evt.EndTime.HasValue)
            {
                var diffEndToEnd = (evt.EndTime.Value - rawEnd).Duration();
                if (diffEndToEnd <= threshold && diffEndToEnd < minEndDiff)
                {
                    minEndDiff = diffEndToEnd;
                    bestEndSnap = evt.EndTime.Value;
                }
            }
        }

        var hasStartSnap = bestStartSnap.HasValue;
        var hasEndSnap = bestEndSnap.HasValue;

        if (hasStartSnap && hasEndSnap)
        {
            if (minStartDiff <= minEndDiff)
            {
                var snappedEnd = bestStartSnap!.Value + duration;
                if (snappedEnd <= maxAllowedEnd)
                {
                    return (bestStartSnap.Value, snappedEnd);
                }

                if (bestEndSnap!.Value <= maxAllowedEnd)
                {
                    return (bestEndSnap.Value - duration, bestEndSnap.Value);
                }
            }
            else
            {
                if (bestEndSnap!.Value <= maxAllowedEnd)
                {
                    return (bestEndSnap.Value - duration, bestEndSnap.Value);
                }

                var snappedEnd = bestStartSnap!.Value + duration;
                if (snappedEnd <= maxAllowedEnd)
                {
                    return (bestStartSnap.Value, snappedEnd);
                }
            }
        }
        else if (hasStartSnap)
        {
            var snappedEnd = bestStartSnap!.Value + duration;
            if (snappedEnd <= maxAllowedEnd)
            {
                return (bestStartSnap.Value, snappedEnd);
            }
        }
        else if (hasEndSnap)
        {
            if (bestEndSnap!.Value <= maxAllowedEnd)
            {
                return (bestEndSnap.Value - duration, bestEndSnap.Value);
            }
        }

        return (rawStart, rawEnd);
    }
}
