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

    [Theory]
    [InlineData(1.0, 600.0, 300.0)] // 1 hour event fills 50% of 600px viewport -> 300 px/hr
    [InlineData(2.0, 600.0, 150.0)] // 2 hour event fills 50% of 600px viewport -> 150 px/hr
    [InlineData(0.5, 600.0, 600.0)] // 30 min event fills 50% of 600px viewport -> 600 px/hr
    [InlineData(0.01, 600.0, 3600.0)] // Clamped to min 5 min (5/60 hr) -> 600 / (2 * 5/60) = 3600 px/hr
    [InlineData(20.0, 600.0, 25.0)] // 20 hour event -> 600 / 40 = 15, clamped to min 600/24 = 25 px/hr
    public void CalculateDoubleClickZoom_CalculatesCorrectZoomAndClamps(double durationHours, double viewportHeight, double expectedZoom)
    {
        double zoom = CalendarTimeScaleHelper.CalculateDoubleClickZoom(durationHours, viewportHeight);

        Assert.Equal(expectedZoom, zoom, 2);
    }

    [Fact]
    public void IsZoomBigEnough_ReturnsTrue_WhenZoomIsAtOrAboveDoubleClickZoom()
    {
        // For a 2 hour event with 600px viewport, double-click zoom is 150 px/hr
        double durationHours = 2.0;
        double viewportHeight = 600.0;

        // Default zoom: 60 px/hr < 150 px/hr -> false
        Assert.False(CalendarTimeScaleHelper.IsZoomBigEnough(60.0, durationHours, viewportHeight));

        // Exact double click zoom: 150 px/hr -> true
        Assert.True(CalendarTimeScaleHelper.IsZoomBigEnough(150.0, durationHours, viewportHeight));

        // Within tolerance (e.g. 149.6 px/hr with 0.5 tolerance) -> true
        Assert.True(CalendarTimeScaleHelper.IsZoomBigEnough(149.6, durationHours, viewportHeight));

        // Well above double click zoom: 300 px/hr -> true
        Assert.True(CalendarTimeScaleHelper.IsZoomBigEnough(300.0, durationHours, viewportHeight));
    }
}
