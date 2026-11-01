using GapMouse.Application.Abstractions;
using GapMouse.Domain.Geometry;

namespace GapMouse.Application.Tests.Fakes;

internal sealed class FakeMonitors : IMonitorBoundsProvider
{
    private int _getCount;

    public int GetCount => Volatile.Read(ref _getCount);

    public ScreenBounds GetBoundsAt(PixelPoint point)
    {
        Interlocked.Increment(ref _getCount);
        return new(0, 0, 1920, 1080);
    }
}
