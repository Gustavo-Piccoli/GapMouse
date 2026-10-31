using GapMouse.Application.Abstractions;
using GapMouse.Domain.Geometry;

namespace GapMouse.Application.Tests.Fakes;

internal sealed class FakeMonitors : IMonitorBoundsProvider
{
    public ScreenBounds GetBoundsAt(PixelPoint point) => new(0, 0, 1920, 1080);
}
