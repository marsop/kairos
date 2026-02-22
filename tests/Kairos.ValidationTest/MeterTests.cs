using Kairos.Shared.Models;

namespace Kairos.ValidationTest;

public class MeterTests
{
    [Theory]
    [InlineData(-10)]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(-1)]
    [InlineData(1.5)]
    public void Factor_IsAlwaysNormalizedToOne(double factor)
    {
        var meter = new Meter
        {
            Name = "Test",
            Factor = factor
        };

        Assert.Equal(1.0, meter.Factor);
    }
}
