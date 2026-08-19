using CarparkAvailability.ApiApp.Services;

namespace CarparkAvailability.Tests;

public sealed class Svy21ConverterTests
{
    [Theory]
    [InlineData(30314.7936, 31490.4942, 1.3011, 103.8541)]
    [InlineData(33758.4143, 33695.5198, 1.3210, 103.8851)]
    [InlineData(28185.4359, 39012.6664, 1.3691, 103.8350)]
    public void Convert_ReturnsExpectedCoordinates(double x, double y, double expectedLat, double expectedLng)
    {
        (double lat, double lng) = Svy21Converter.Convert(x, y);

        Assert.InRange(Math.Abs(lat - expectedLat), 0, 0.001);
        Assert.InRange(Math.Abs(lng - expectedLng), 0, 0.001);
    }
}
