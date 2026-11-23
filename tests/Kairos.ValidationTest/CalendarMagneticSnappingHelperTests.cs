namespace Kairos.ValidationTest;

using System;
using System.Collections.Generic;
using Kairos.Application.Common;
using Kairos.Core.Models;
using Xunit;

public class CalendarMagneticSnappingHelperTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 11, 23, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SnapPoint_ThresholdZeroOrNegative_ReturnsCandidateUnchanged()
    {
        var candidate = BaseTime.AddMinutes(5);
        var events = new[]
        {
            new ActivityEvent { Id = Guid.NewGuid(), StartTime = BaseTime, EndTime = BaseTime.AddMinutes(6) }
        };

        var resultZero = CalendarMagneticSnappingHelper.SnapPoint(candidate, events, TimeSpan.Zero);
        var resultNegative = CalendarMagneticSnappingHelper.SnapPoint(candidate, events, TimeSpan.FromSeconds(-10));

        Assert.Equal(candidate, resultZero);
        Assert.Equal(candidate, resultNegative);
    }

    [Fact]
    public void SnapPoint_NullReferenceEvents_ReturnsCandidateUnchanged()
    {
        var candidate = BaseTime.AddMinutes(5);
        var result = CalendarMagneticSnappingHelper.SnapPoint(candidate, null, TimeSpan.FromMinutes(2));

        Assert.Equal(candidate, result);
    }

    [Fact]
    public void SnapPoint_CandidateWithinThresholdOfStartTime_SnapsToStartTime()
    {
        var targetEvent = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            StartTime = BaseTime.AddMinutes(10),
            EndTime = BaseTime.AddMinutes(20)
        };
        var candidate = BaseTime.AddMinutes(9); // 1 minute away

        var result = CalendarMagneticSnappingHelper.SnapPoint(
            candidate,
            new[] { targetEvent },
            TimeSpan.FromMinutes(2));

        Assert.Equal(targetEvent.StartTime, result);
    }

    [Fact]
    public void SnapPoint_CandidateWithinThresholdOfEndTime_SnapsToEndTime()
    {
        var targetEvent = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            StartTime = BaseTime,
            EndTime = BaseTime.AddMinutes(30)
        };
        var candidate = BaseTime.AddMinutes(32); // 2 minutes away

        var result = CalendarMagneticSnappingHelper.SnapPoint(
            candidate,
            new[] { targetEvent },
            TimeSpan.FromMinutes(5));

        Assert.Equal(targetEvent.EndTime!.Value, result);
    }

    [Fact]
    public void SnapPoint_MultipleBoundariesWithinThreshold_SelectsClosestBoundary()
    {
        var event1 = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            StartTime = BaseTime,
            EndTime = BaseTime.AddMinutes(10)
        };
        var event2 = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            StartTime = BaseTime.AddMinutes(12),
            EndTime = BaseTime.AddMinutes(25)
        };

        // Candidate at 10:11 (1 min from event1.EndTime, 1 min from event2.StartTime)
        // Candidate at 10:11:30 (1.5 min from event1.EndTime, 0.5 min from event2.StartTime) -> closest is event2.StartTime
        var candidate = BaseTime.AddMinutes(11.5);

        var result = CalendarMagneticSnappingHelper.SnapPoint(
            candidate,
            new[] { event1, event2 },
            TimeSpan.FromMinutes(5));

        Assert.Equal(event2.StartTime, result);
    }

    [Fact]
    public void SnapPoint_ExcludesEventMatchingExcludedId()
    {
        var excludedId = Guid.NewGuid();
        var selfEvent = new ActivityEvent
        {
            Id = excludedId,
            StartTime = BaseTime,
            EndTime = BaseTime.AddMinutes(15)
        };
        var candidate = BaseTime.AddMinutes(14);

        var result = CalendarMagneticSnappingHelper.SnapPoint(
            candidate,
            new[] { selfEvent },
            TimeSpan.FromMinutes(5),
            excludedEventId: excludedId);

        Assert.Equal(candidate, result);
    }

    [Fact]
    public void SnapPoint_CandidateOutsideThreshold_ReturnsCandidateUnchanged()
    {
        var targetEvent = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            StartTime = BaseTime,
            EndTime = BaseTime.AddMinutes(10)
        };
        var candidate = BaseTime.AddMinutes(20); // 10 minutes away, threshold is 3 minutes

        var result = CalendarMagneticSnappingHelper.SnapPoint(
            candidate,
            new[] { targetEvent },
            TimeSpan.FromMinutes(3));

        Assert.Equal(candidate, result);
    }

    [Fact]
    public void SnapResizeStart_WithinThresholdAndBeforeCurrentEnd_SnapsCorrectly()
    {
        var prevEvent = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            StartTime = BaseTime,
            EndTime = BaseTime.AddMinutes(30)
        };
        var candidateStart = BaseTime.AddMinutes(32);
        var currentEnd = BaseTime.AddHours(1);

        var result = CalendarMagneticSnappingHelper.SnapResizeStart(
            candidateStart,
            currentEnd,
            new[] { prevEvent },
            TimeSpan.FromMinutes(5));

        Assert.Equal(prevEvent.EndTime!.Value, result);
    }

    [Fact]
    public void SnapResizeStart_SnappedBoundaryAtOrAfterCurrentEnd_RejectsSnap()
    {
        var otherEvent = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            StartTime = BaseTime.AddMinutes(45),
            EndTime = BaseTime.AddMinutes(50)
        };
        var candidateStart = BaseTime.AddMinutes(44);
        var currentEnd = BaseTime.AddMinutes(40); // End is earlier than candidate/target

        var result = CalendarMagneticSnappingHelper.SnapResizeStart(
            candidateStart,
            currentEnd,
            new[] { otherEvent },
            TimeSpan.FromMinutes(5));

        Assert.Equal(candidateStart, result);
    }

    [Fact]
    public void SnapResizeEnd_WithinThresholdAndAfterCurrentStart_SnapsCorrectly()
    {
        var nextEvent = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            StartTime = BaseTime.AddHours(2),
            EndTime = BaseTime.AddHours(3)
        };
        var currentStart = BaseTime.AddHours(1);
        var candidateEnd = BaseTime.AddHours(2).AddMinutes(-2);
        var maxAllowed = BaseTime.AddHours(5);

        var result = CalendarMagneticSnappingHelper.SnapResizeEnd(
            currentStart,
            candidateEnd,
            new[] { nextEvent },
            TimeSpan.FromMinutes(5),
            maxAllowed);

        Assert.Equal(nextEvent.StartTime, result);
    }

    [Fact]
    public void SnapResizeEnd_SnappedBoundaryAtOrBeforeCurrentStart_RejectsSnap()
    {
        var otherEvent = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            StartTime = BaseTime.AddMinutes(10),
            EndTime = BaseTime.AddMinutes(20)
        };
        var currentStart = BaseTime.AddMinutes(25);
        var candidateEnd = BaseTime.AddMinutes(21); // Snap would be 20, which is < currentStart 25
        var maxAllowed = BaseTime.AddHours(1);

        var result = CalendarMagneticSnappingHelper.SnapResizeEnd(
            currentStart,
            candidateEnd,
            new[] { otherEvent },
            TimeSpan.FromMinutes(5),
            maxAllowed);

        Assert.Equal(candidateEnd, result);
    }

    [Fact]
    public void SnapResizeEnd_SnappedBoundaryInFuture_RejectsSnap()
    {
        var futureTarget = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            StartTime = BaseTime.AddHours(2),
            EndTime = BaseTime.AddHours(3)
        };
        var currentStart = BaseTime;
        var candidateEnd = BaseTime.AddHours(2).AddMinutes(-1);
        var maxAllowed = BaseTime.AddHours(1); // Future limit before target

        var result = CalendarMagneticSnappingHelper.SnapResizeEnd(
            currentStart,
            candidateEnd,
            new[] { futureTarget },
            TimeSpan.FromMinutes(5),
            maxAllowed);

        Assert.Equal(candidateEnd, result);
    }

    [Fact]
    public void SnapMove_CloserToStartBoundary_PreservesDurationAndSnapsStart()
    {
        var prevEvent = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            StartTime = BaseTime,
            EndTime = BaseTime.AddMinutes(30)
        };
        var duration = TimeSpan.FromMinutes(45);
        var rawStart = BaseTime.AddMinutes(32); // 2 mins from prevEvent.EndTime
        var rawEnd = rawStart + duration;
        var maxAllowed = BaseTime.AddHours(5);

        var (snappedStart, snappedEnd) = CalendarMagneticSnappingHelper.SnapMove(
            rawStart,
            rawEnd,
            new[] { prevEvent },
            TimeSpan.FromMinutes(5),
            maxAllowed);

        Assert.Equal(prevEvent.EndTime!.Value, snappedStart);
        Assert.Equal(prevEvent.EndTime!.Value + duration, snappedEnd);
        Assert.Equal(duration, snappedEnd - snappedStart);
    }

    [Fact]
    public void SnapMove_CloserToEndBoundary_PreservesDurationAndSnapsEnd()
    {
        var nextEvent = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            StartTime = BaseTime.AddHours(2),
            EndTime = BaseTime.AddHours(3)
        };
        var duration = TimeSpan.FromMinutes(30);
        var rawEnd = BaseTime.AddHours(2).AddMinutes(-1); // 1 min from nextEvent.StartTime
        var rawStart = rawEnd - duration;
        var maxAllowed = BaseTime.AddHours(5);

        var (snappedStart, snappedEnd) = CalendarMagneticSnappingHelper.SnapMove(
            rawStart,
            rawEnd,
            new[] { nextEvent },
            TimeSpan.FromMinutes(5),
            maxAllowed);

        Assert.Equal(nextEvent.StartTime, snappedEnd);
        Assert.Equal(nextEvent.StartTime - duration, snappedStart);
        Assert.Equal(duration, snappedEnd - snappedStart);
    }

    [Fact]
    public void SnapMove_SnappedEndExceedsMaxAllowedEnd_FallsBackToEndSnapOrRaw()
    {
        var prevEvent = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            StartTime = BaseTime,
            EndTime = BaseTime.AddMinutes(30)
        };
        var duration = TimeSpan.FromMinutes(60);
        var rawStart = BaseTime.AddMinutes(31);
        var rawEnd = rawStart + duration;
        var maxAllowed = BaseTime.AddMinutes(60); // Snapped end would be 30 + 60 = 90 > 60

        var (snappedStart, snappedEnd) = CalendarMagneticSnappingHelper.SnapMove(
            rawStart,
            rawEnd,
            new[] { prevEvent },
            TimeSpan.FromMinutes(5),
            maxAllowed);

        Assert.Equal(rawStart, snappedStart);
        Assert.Equal(rawEnd, snappedEnd);
    }

    [Fact]
    public void SnapMove_ZeroOrNegativeThreshold_ReturnsRawUnchanged()
    {
        var prevEvent = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            StartTime = BaseTime,
            EndTime = BaseTime.AddMinutes(30)
        };
        var rawStart = BaseTime.AddMinutes(31);
        var rawEnd = rawStart.AddMinutes(30);

        var (snappedStart, snappedEnd) = CalendarMagneticSnappingHelper.SnapMove(
            rawStart,
            rawEnd,
            new[] { prevEvent },
            TimeSpan.Zero,
            BaseTime.AddHours(5));

        Assert.Equal(rawStart, snappedStart);
        Assert.Equal(rawEnd, snappedEnd);
    }
}
