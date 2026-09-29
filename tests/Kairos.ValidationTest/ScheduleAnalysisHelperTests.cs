using Kairos.Application.Common;
using Kairos.Core.Models;
using Xunit;

namespace Kairos.ValidationTest;

public class ScheduleAnalysisHelperTests
{
    private static readonly DateTimeOffset BaseDate = new(2026, 9, 29, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Analyze_EmptyEvents_ReturnsEmptyGapsAndOverlaps()
    {
        var result = ScheduleAnalysisHelper.Analyze(Array.Empty<ActivityEvent>());

        Assert.Empty(result.Gaps);
        Assert.Empty(result.Overlaps);
        Assert.Empty(result.GapsBeforeEvent);
    }

    [Fact]
    public void Analyze_SingleEvent_ReturnsEmptyGapsAndOverlaps()
    {
        var singleEvent = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            ActivityName = "Focus",
            StartTime = BaseDate,
            EndTime = BaseDate.AddHours(1)
        };

        var result = ScheduleAnalysisHelper.Analyze(new[] { singleEvent });

        Assert.Empty(result.Gaps);
        Assert.Empty(result.Overlaps);
        Assert.Empty(result.GapsBeforeEvent);
    }

    [Fact]
    public void Analyze_SeamlessConsecutiveEvents_ReturnsNoGapsAndNoOverlaps()
    {
        var e1 = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            ActivityName = "Design",
            StartTime = BaseDate,
            EndTime = BaseDate.AddHours(1)
        };
        var e2 = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            ActivityName = "Development",
            StartTime = BaseDate.AddHours(1),
            EndTime = BaseDate.AddHours(2)
        };

        var result = ScheduleAnalysisHelper.Analyze(new[] { e1, e2 });

        Assert.Empty(result.Gaps);
        Assert.Empty(result.Overlaps);
        Assert.Empty(result.GapsBeforeEvent);
    }

    [Fact]
    public void Analyze_GapLessThanOneMinute_Ignored()
    {
        var e1 = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            ActivityName = "Design",
            StartTime = BaseDate,
            EndTime = BaseDate.AddHours(1)
        };
        var e2 = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            ActivityName = "Development",
            StartTime = BaseDate.AddHours(1).AddSeconds(45), // 45 seconds gap
            EndTime = BaseDate.AddHours(2)
        };

        var result = ScheduleAnalysisHelper.Analyze(new[] { e1, e2 });

        Assert.Empty(result.Gaps);
        Assert.Empty(result.GapsBeforeEvent);
    }

    [Theory]
    [InlineData(1)]  // Lower threshold boundary (1 minute)
    [InlineData(5)]  // Mid-range gap (5 minutes)
    [InlineData(10)] // Upper threshold boundary (10 minutes)
    public void Analyze_GapBetweenOneAndTenMinutes_DetectedCorrectly(int gapMinutes)
    {
        var e1 = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            ActivityName = "Design",
            StartTime = BaseDate,
            EndTime = BaseDate.AddHours(1)
        };
        var e2 = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            ActivityName = "Development",
            StartTime = BaseDate.AddHours(1).AddMinutes(gapMinutes),
            EndTime = BaseDate.AddHours(2)
        };

        var result = ScheduleAnalysisHelper.Analyze(new[] { e1, e2 });

        Assert.Single(result.Gaps);
        var gap = result.Gaps[0];
        Assert.Equal(e1.EndTime, gap.StartTime);
        Assert.Equal(e2.StartTime, gap.EndTime);
        Assert.Equal(TimeSpan.FromMinutes(gapMinutes), gap.Duration);

        Assert.True(result.GapsBeforeEvent.ContainsKey(e2.Id));
        Assert.Equal(gap, result.GapsBeforeEvent[e2.Id]);
    }

    [Fact]
    public void Analyze_GapGreaterThanTenMinutes_IgnoredAsIntentionalBreak()
    {
        var e1 = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            ActivityName = "Design",
            StartTime = BaseDate,
            EndTime = BaseDate.AddHours(1)
        };
        var e2 = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            ActivityName = "Development",
            StartTime = BaseDate.AddHours(1).AddMinutes(15), // 15 mins > 10 mins
            EndTime = BaseDate.AddHours(2)
        };

        var result = ScheduleAnalysisHelper.Analyze(new[] { e1, e2 });

        Assert.Empty(result.Gaps);
        Assert.Empty(result.GapsBeforeEvent);
    }

    [Fact]
    public void Analyze_OverlappingEvents_DetectedWithCorrectIdsAndNames()
    {
        var e1 = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            ActivityName = "Meeting",
            StartTime = BaseDate,
            EndTime = BaseDate.AddHours(1)
        };
        var e2 = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            ActivityName = "Urgent Support",
            StartTime = BaseDate.AddMinutes(30),
            EndTime = BaseDate.AddMinutes(90)
        };

        var result = ScheduleAnalysisHelper.Analyze(new[] { e1, e2 });

        Assert.Empty(result.Gaps);
        Assert.Equal(2, result.Overlaps.Count);

        Assert.True(result.Overlaps.ContainsKey(e1.Id));
        var overlap1 = result.Overlaps[e1.Id];
        Assert.Contains(e2.Id, overlap1.OverlappingEventIds);
        Assert.Contains("Urgent Support", overlap1.OverlappingActivityNames);

        Assert.True(result.Overlaps.ContainsKey(e2.Id));
        var overlap2 = result.Overlaps[e2.Id];
        Assert.Contains(e1.Id, overlap2.OverlappingEventIds);
        Assert.Contains("Meeting", overlap2.OverlappingActivityNames);
    }

    [Fact]
    public void Analyze_OverlappingEventsFollowedByGap_CalculatesCorrectGapFromLatestCoverage()
    {
        var e1 = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            ActivityName = "Task A",
            StartTime = BaseDate,
            EndTime = BaseDate.AddHours(1) // Ends at 10:00
        };
        var e2 = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            ActivityName = "Task B",
            StartTime = BaseDate.AddMinutes(30),
            EndTime = BaseDate.AddMinutes(75) // Ends at 10:15
        };
        var e3 = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            ActivityName = "Task C",
            StartTime = BaseDate.AddMinutes(80), // Starts at 10:20 (5m gap after 10:15)
            EndTime = BaseDate.AddHours(2)
        };

        var result = ScheduleAnalysisHelper.Analyze(new[] { e1, e2, e3 });

        // Overlaps for e1 and e2
        Assert.True(result.Overlaps.ContainsKey(e1.Id));
        Assert.True(result.Overlaps.ContainsKey(e2.Id));
        Assert.False(result.Overlaps.ContainsKey(e3.Id));

        // Gap between 10:15 and 10:20 (5 minutes) before e3
        Assert.Single(result.Gaps);
        var gap = result.Gaps[0];
        Assert.Equal(BaseDate.AddMinutes(75), gap.StartTime);
        Assert.Equal(BaseDate.AddMinutes(80), gap.EndTime);
        Assert.Equal(TimeSpan.FromMinutes(5), gap.Duration);
        Assert.True(result.GapsBeforeEvent.ContainsKey(e3.Id));
    }

    [Fact]
    public void Analyze_ActiveEvent_EvaluatesWithEffectiveNow()
    {
        var effectiveNow = BaseDate.AddHours(1).AddMinutes(5);

        var e1 = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            ActivityName = "Planning",
            StartTime = BaseDate,
            EndTime = BaseDate.AddHours(1)
        };
        var e2 = new ActivityEvent
        {
            Id = Guid.NewGuid(),
            ActivityName = "Coding",
            StartTime = BaseDate.AddHours(1).AddMinutes(5),
            EndTime = null // Active
        };

        var result = ScheduleAnalysisHelper.Analyze(new[] { e1, e2 }, effectiveNow);

        // 5 minute gap between e1 end and e2 start
        Assert.Single(result.Gaps);
        Assert.Equal(TimeSpan.FromMinutes(5), result.Gaps[0].Duration);
        Assert.Empty(result.Overlaps);
    }

    [Fact]
    public void ScheduleGap_GetStartSecondsAndGetEndSeconds_ClampsCorrectly()
    {
        var date = new DateOnly(2026, 9, 29);
        var gapStart = new DateTimeOffset(2026, 9, 29, 10, 0, 0, DateTimeOffset.Now.Offset);
        var gapEnd = new DateTimeOffset(2026, 9, 29, 10, 5, 0, DateTimeOffset.Now.Offset);
        var gap = new ScheduleGap(gapStart, gapEnd, TimeSpan.FromMinutes(5));

        Assert.Equal(10 * 3600, gap.GetStartSeconds(date));
        Assert.Equal(10 * 3600 + 5 * 60, gap.GetEndSeconds(date));
    }
}
