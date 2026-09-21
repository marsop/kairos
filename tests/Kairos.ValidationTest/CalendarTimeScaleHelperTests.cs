using Kairos.Application.Common;
using Xunit;

namespace Kairos.ValidationTest;

public class CalendarTimeScaleHelperTests
{
    [Fact]
    public void GetTimeIntervalMinutes_AtMaxZoom_SelectsOneMinute()
    {
        // 3600 px/hr with 600px viewport (10 minutes fills the screen)
        double pixelsPerHour = 3600;
        double viewportHeight = 600;

        int interval = CalendarTimeScaleHelper.GetTimeIntervalMinutes(pixelsPerHour, viewportHeight);

        Assert.Equal(1, interval);
    }

    [Fact]
    public void GetTimeMarkers_AtOneMinuteInterval_IncludesSequentialMinuteLabels()
    {
        double pixelsPerHour = 3600;
        double viewportHeight = 600;

        var markers = CalendarTimeScaleHelper.GetTimeMarkers(pixelsPerHour, viewportHeight);

        Assert.NotEmpty(markers);
        // Find markers around 10:34, 10:35, 10:36
        var marker1034 = markers.FirstOrDefault(m => m.Label == "10:34");
        var marker1035 = markers.FirstOrDefault(m => m.Label == "10:35");
        var marker1036 = markers.FirstOrDefault(m => m.Label == "10:36");

        Assert.NotNull(marker1034);
        Assert.NotNull(marker1035);
        Assert.NotNull(marker1036);
        Assert.Equal(10 * 3600 + 34 * 60, marker1034.Seconds);
        Assert.Equal(10 * 3600 + 35 * 60, marker1035.Seconds);
        Assert.Equal(10 * 3600 + 36 * 60, marker1036.Seconds);
        Assert.False(marker1034.IsMajorHour);
        Assert.False(marker1035.IsMajorHour);
        Assert.False(marker1036.IsMajorHour);

        // Check hour marker
        var marker1100 = markers.FirstOrDefault(m => m.Label == "11:00");
        Assert.NotNull(marker1100);
        Assert.True(marker1100.IsMajorHour);
    }

    [Theory]
    [InlineData(3600, 600, 1)]   // Max zoom: 1 min
    [InlineData(1800, 600, 2)]   // High zoom: 2 min
    [InlineData(900, 600, 5)]    // Zoomed in: 5 min
    [InlineData(480, 600, 10)]   // Medium zoom: 10 min
    [InlineData(240, 600, 15)]   // Low zoom: 15 min
    [InlineData(120, 600, 30)]   // Moderate: 30 min
    [InlineData(60, 600, 60)]    // Default zoom: 60 min (1 hr)
    public void GetTimeIntervalMinutes_SelectsAppropriateIntervalsForZoomLevels(double pixelsPerHour, double viewportHeight, int expectedInterval)
    {
        int interval = CalendarTimeScaleHelper.GetTimeIntervalMinutes(pixelsPerHour, viewportHeight);

        Assert.Equal(expectedInterval, interval);
    }

    [Theory]
    [InlineData(25, 600)]
    [InlineData(60, 600)]
    [InlineData(120, 600)]
    [InlineData(240, 600)]
    [InlineData(480, 600)]
    [InlineData(900, 600)]
    [InlineData(1800, 600)]
    [InlineData(3600, 600)]
    public void GetTimeIntervalMinutes_MaintainsMinimumPixelSpacing(double pixelsPerHour, double viewportHeight)
    {
        const double minSpacingPx = 25.0;
        int interval = CalendarTimeScaleHelper.GetTimeIntervalMinutes(pixelsPerHour, viewportHeight, minLabelCount: 6, minSpacingPx: minSpacingPx);

        double actualSpacing = (interval / 60.0) * pixelsPerHour;
        Assert.True(actualSpacing >= minSpacingPx, $"Spacing {actualSpacing}px for interval {interval}m should be >= {minSpacingPx}px");
    }

    [Theory]
    [InlineData(60, 600, 6)]
    [InlineData(120, 600, 6)]
    [InlineData(240, 600, 6)]
    [InlineData(480, 600, 6)]
    [InlineData(900, 600, 6)]
    [InlineData(1800, 600, 6)]
    [InlineData(3600, 600, 6)]
    public void GetTimeIntervalMinutes_EnsuresMinimumVisibleLabelCount(double pixelsPerHour, double viewportHeight, int minLabelCount)
    {
        int interval = CalendarTimeScaleHelper.GetTimeIntervalMinutes(pixelsPerHour, viewportHeight, minLabelCount);

        double actualSpacing = (interval / 60.0) * pixelsPerHour;
        double visibleLabels = viewportHeight / actualSpacing;

        Assert.True(visibleLabels >= minLabelCount, $"Visible labels count {visibleLabels} should be >= {minLabelCount}");
    }

    [Fact]
    public void GetTimeIntervalMinutes_WithZeroOrNegativeInputs_UsesFallbacks()
    {
        int interval = CalendarTimeScaleHelper.GetTimeIntervalMinutes(0, 0, 0, 0);

        Assert.InRange(interval, 1, 120);
    }

    [Fact]
    public void GetTimeMarkers_FullDay_StartsAtMidnightAndEndsBeforeNextDay()
    {
        var markers = CalendarTimeScaleHelper.GetTimeMarkers(60, 600);

        Assert.NotEmpty(markers);
        Assert.Equal("00:00", markers.First().Label);
        Assert.Equal(0, markers.First().Seconds);
        Assert.True(markers.First().IsMajorHour);
        Assert.True(markers.Last().Seconds < 24 * 3600);
    }
}
