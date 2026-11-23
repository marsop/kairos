using Kairos.Core.Models;

namespace Kairos.ValidationTest;

public class CoreModelTests
{
    [Fact]
    public void Activity_NormalizeColor_ValidHex_ReturnsUppercase()
    {
        var result = Activity.NormalizeColor("#abcdef");
        Assert.Equal("#ABCDEF", result);

        var result2 = Activity.NormalizeColor("#10b981");
        Assert.Equal("#10B981", result2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("1234567")]
    [InlineData("#12345Z")]
    [InlineData("#GGGGGG")]
    public void Activity_NormalizeColor_InvalidHex_ThrowsArgumentException(string? invalidColor)
    {
        Assert.Throws<ArgumentException>(() => Activity.NormalizeColor(invalidColor));
    }

    [Fact]
    public void Activity_SanitizeColor_ValidHex_ReturnsUppercase()
    {
        var result = Activity.SanitizeColor("#3b82f6");
        Assert.Equal("#3B82F6", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("#123")]
    public void Activity_SanitizeColor_InvalidHex_ReturnsDefaultColor(string? invalidColor)
    {
        var result = Activity.SanitizeColor(invalidColor);
        Assert.Equal(Activity.DefaultColor, result);
    }

    [Fact]
    public void Activity_DefaultValues_AreCorrect()
    {
        var act = new Activity();
        Assert.NotEqual(Guid.Empty, act.Id);
        Assert.Equal(string.Empty, act.Emoji);
        Assert.Equal(string.Empty, act.Name);
        Assert.Equal(Activity.DefaultColor, act.Color);
        Assert.Equal(0, act.DisplayOrder);
        Assert.Equal(0, act.ActivityGroupId);
    }

    [Fact]
    public void TimeAccount_DefaultValues_AreCorrect()
    {
        var ta = new TimeAccount();
        Assert.NotNull(ta.Events);
        Assert.NotNull(ta.Activities);
        Assert.Equal(TimeSpan.FromHours(24), ta.TimelinePeriod);
    }

    [Fact]
    public void ActivityBudget_DefaultValues_AreCorrect()
    {
        var b = new ActivityBudget();
        Assert.NotEqual(Guid.Empty, b.Id);
        Assert.Equal(Guid.Empty, b.ActivityId);
        Assert.Equal(TimeSpan.Zero, b.AllocatedTimeSpan);
        Assert.Equal(TimeSpan.Zero, b.MinimumTimeSpan);
        Assert.Equal(BudgetType.Monthly, b.Type);
    }

    [Fact]
    public void ActivityEvent_PropertiesAndDefaults()
    {
        var evt = new ActivityEvent();
        Assert.NotEqual(Guid.Empty, evt.Id);
        Assert.Null(evt.EndTime);
        Assert.True(evt.IsActive);
        Assert.Equal(string.Empty, evt.Comment);

        evt.EndTime = evt.StartTime.AddHours(1);
        Assert.False(evt.IsActive);
        Assert.Equal(TimeSpan.FromHours(1), evt.Duration);
    }

    [Fact]
    public void ActivityEventQueryOptions_Properties()
    {
        var opts = new ActivityEventQueryOptions
        {
            From = DateTimeOffset.UtcNow.AddDays(-1),
            To = DateTimeOffset.UtcNow,
            Since = DateTimeOffset.UtcNow.AddHours(-2),
            Limit = 10,
            Offset = 20
        };

        Assert.NotNull(opts.From);
        Assert.NotNull(opts.To);
        Assert.NotNull(opts.Since);
        Assert.Equal(10, opts.Limit);
        Assert.Equal(20, opts.Offset);
    }

    [Fact]
    public void AvatarProfile_Properties()
    {
        var profile = new AvatarProfile("classic", "Classic", "img/avatars/classic", 5);

        Assert.Equal("classic", profile.Id);
        Assert.Equal("Classic", profile.Name);
        Assert.Equal("img/avatars/classic", profile.BasePath);
        Assert.Equal(5, profile.ImageCount);
    }
}
