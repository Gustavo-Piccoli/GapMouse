using GapMouse.Domain.Geometry;

namespace GapMouse.Application.Abstractions;

/// <summary>Resolves the bounds of the monitor containing a point.</summary>
public interface IMonitorBoundsProvider
{
    /// <summary>Returns the bounds of the monitor nearest to the given point.</summary>
    ScreenBounds GetBoundsAt(PixelPoint point);
}
