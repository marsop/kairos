using Budgetr.Shared.Models;

namespace Budgetr.ValidationTest;

public class MeterTests
{
    [Theory]
    [InlineData(-10)]
    [InlineData(0)]
    [InlineData(10)]
    public void Factor_InRange_IsAccepted(double factor)
    {
        var meter = new Meter
        {
            Name = "Test",
            Factor = factor
        };

        Assert.Equal(factor, meter.Factor);
    }

    [Theory]
    [InlineData(-10.1)]
    [InlineData(10.1)]
    public void Factor_OutOfRange_Throws(double factor)
    {
        var meter = new Meter();
        Assert.Throws<ArgumentOutOfRangeException>(() => meter.Factor = factor);
    }
}
