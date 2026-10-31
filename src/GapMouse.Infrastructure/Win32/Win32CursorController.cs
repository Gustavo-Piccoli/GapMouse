using System.ComponentModel;
using System.Runtime.InteropServices;
using GapMouse.Application.Abstractions;
using GapMouse.Domain.Geometry;

namespace GapMouse.Infrastructure.Win32;

public sealed class Win32CursorController : ICursorController
{
    public PixelPoint GetPosition()
    {
        if (!NativeMethods.GetCursorPos(out var point))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return new PixelPoint(point.X, point.Y);
    }

    public bool TrySetPosition(PixelPoint position) => NativeMethods.SetCursorPos(position.X, position.Y);
}
