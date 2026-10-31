namespace GapMouse.Domain.Geometry;

/// <summary>A monitor rectangle in virtual-screen coordinates. Right and Bottom are exclusive, as in Win32 RECT.</summary>
public readonly record struct ScreenBounds
{
    public ScreenBounds(int left, int top, int right, int bottom)
    {
        if (right <= left || bottom <= top)
        {
            throw new ArgumentException("Bounds must have a positive width and height.");
        }

        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    public int Left { get; }

    public int Top { get; }

    public int Right { get; }

    public int Bottom { get; }

    /// <summary>Returns the closest point that lies inside the bounds.</summary>
    public PixelPoint Clamp(PixelPoint p) =>
        new(Math.Clamp(p.X, Left, Right - 1), Math.Clamp(p.Y, Top, Bottom - 1));
}
