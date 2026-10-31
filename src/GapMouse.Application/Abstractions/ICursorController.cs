using GapMouse.Domain.Geometry;

namespace GapMouse.Application.Abstractions;

/// <summary>Reads and moves the system cursor.</summary>
public interface ICursorController
{
    /// <summary>Returns the current cursor position in screen pixels.</summary>
    PixelPoint GetPosition();

    /// <summary>Moves the cursor; returns false if the system refused the move.</summary>
    bool TrySetPosition(PixelPoint position);
}
