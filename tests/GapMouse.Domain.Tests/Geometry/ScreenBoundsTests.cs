using GapMouse.Domain.Geometry;

namespace GapMouse.Domain.Tests.Geometry;

public class ScreenBoundsTests
{
    [Theory]
    [InlineData(-1920, 0, -1920, 0)] // secondary monitor left of primary: point at its left edge stays put
    [InlineData(-5000, 500, -1920, 500)] // far left, clamped to Left and not to 0
    [InlineData(-100, 2000, -100, 1079)] // below bottom, clamped to Bottom-1
    [InlineData(100, 500, -1, 500)] // beyond the exclusive right edge, clamped to Right-1
    public void Clamp_PointOutsideMonitor_KeepsPointInsideMonitor(int x, int y, int expectedX, int expectedY) =>
        new ScreenBounds(-1920, 0, 0, 1080).Clamp(new PixelPoint(x, y)).ShouldBe(new PixelPoint(expectedX, expectedY));

    [Fact]
    public void Ctor_EmptyRectangle_Throws() =>
        Should.Throw<ArgumentException>(() => new ScreenBounds(0, 0, 0, 10));
}
