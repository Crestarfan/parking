using CarparkAvailability.ApiApp.Services;

namespace CarparkAvailability.Tests;

public sealed class HaversineDistanceTests
{
    [Fact]
    public void Calculate_ReturnsZeroForSamePoint()
    {
        double distance = HaversineDistance.Calculate(1.30, 103.85, 1.30, 103.85);

        Assert.Equal(0, distance, precision: 6);
    }

    [Fact]
    public void Calculate_IsSymmetric()
    {
        double forward = HaversineDistance.Calculate(1.3010, 103.8518, 1.3209, 103.8833);
        double reverse = HaversineDistance.Calculate(1.3209, 103.8833, 1.3010, 103.8518);

        Assert.Equal(forward, reverse, precision: 6);
    }

    [Fact]
    public void Calculate_ApproximatesOneKilometre()
    {
        double distance = HaversineDistance.Calculate(0, 0, 0.0089932, 0);

        Assert.InRange(distance, 995, 1005);
    }
}
