using Kairos.Application.Common;
using Xunit;

namespace Kairos.ValidationTest;

public class DateHelperTests
{
    [Theory]
    [InlineData(2026, 10, 19, DayOfWeek.Monday, 0)]    // Monday -> same day
    [InlineData(2026, 10, 20, DayOfWeek.Tuesday, 1)]   // Tuesday -> previous day (Monday)
    [InlineData(2026, 10, 21, DayOfWeek.Wednesday, 2)] // Wednesday -> 2 days back
    [InlineData(2026, 10, 22, DayOfWeek.Thursday, 3)]  // Thursday -> 3 days back
    [InlineData(2026, 10, 23, DayOfWeek.Friday, 4)]    // Friday -> 4 days back
    [InlineData(2026, 10, 24, DayOfWeek.Saturday, 5)]  // Saturday -> 5 days back
    [InlineData(2026, 10, 25, DayOfWeek.Sunday, 6)]    // Sunday -> 6 days back (not next Monday!)
    public void GetStartOfWeek_DateOnly_ReturnsPrecedingMonday(int year, int month, int day, DayOfWeek expectedDayOfWeek, int daysAfterMonday)
    {
        var inputDate = new DateOnly(year, month, day);
        Assert.Equal(expectedDayOfWeek, inputDate.DayOfWeek);

        var expectedMonday = new DateOnly(2026, 10, 19);
        Assert.Equal(daysAfterMonday, inputDate.DayNumber - expectedMonday.DayNumber);
        var actualMonday = DateHelper.GetStartOfWeek(inputDate);

        Assert.Equal(expectedMonday, actualMonday);
        Assert.Equal(DayOfWeek.Monday, actualMonday.DayOfWeek);
    }

    [Fact]
    public void GetStartOfWeek_Sunday_DoesNotJumpToNextWeek()
    {
        var sunday = new DateOnly(2026, 10, 25);
        Assert.Equal(DayOfWeek.Sunday, sunday.DayOfWeek);

        var startOfWeek = DateHelper.GetStartOfWeek(sunday);

        Assert.Equal(new DateOnly(2026, 10, 19), startOfWeek);
        Assert.True(startOfWeek <= sunday);
    }

    [Fact]
    public void GetStartOfWeek_DateTime_ReturnsMidnightMonday()
    {
        var wednesday = new DateTime(2026, 10, 21, 15, 30, 45);
        var startOfWeek = DateHelper.GetStartOfWeek(wednesday);

        Assert.Equal(new DateTime(2026, 10, 19, 0, 0, 0), startOfWeek);
        Assert.Equal(DayOfWeek.Monday, startOfWeek.DayOfWeek);
        Assert.Equal(TimeSpan.Zero, startOfWeek.TimeOfDay);
    }

    [Fact]
    public void GetStartOfWeek_DateTimeOffset_ReturnsUtcMidnightMonday()
    {
        var sunday = new DateTimeOffset(2026, 10, 25, 23, 59, 59, TimeSpan.FromHours(2));
        var startOfWeek = DateHelper.GetStartOfWeek(sunday);

        Assert.Equal(new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero), startOfWeek);
        Assert.Equal(DayOfWeek.Monday, startOfWeek.DayOfWeek);
    }

    [Fact]
    public void GetStartOfWeek_YearBoundary_CorrectlyCalculatesMonday()
    {
        // 2027-01-03 is a Sunday; the Monday of that week was 2026-12-28
        var sundayJan3 = new DateOnly(2027, 1, 3);
        Assert.Equal(DayOfWeek.Sunday, sundayJan3.DayOfWeek);

        var startOfWeek = DateHelper.GetStartOfWeek(sundayJan3);

        Assert.Equal(new DateOnly(2026, 12, 28), startOfWeek);
        Assert.Equal(DayOfWeek.Monday, startOfWeek.DayOfWeek);
    }
}
