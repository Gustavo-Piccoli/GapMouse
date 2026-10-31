using System.ComponentModel;
using System.Runtime.InteropServices;
using GapMouse.Application.Abstractions;
using GapMouse.Domain.Geometry;

namespace GapMouse.Infrastructure.Win32;

public sealed class Win32MonitorBoundsProvider : IMonitorBoundsProvider
{
    public unsafe ScreenBounds GetBoundsAt(PixelPoint point)
    {
        var monitor = NativeMethods.MonitorFromPoint(
            new NativeMethods.POINT { X = point.X, Y = point.Y },
            NativeMethods.MONITOR_DEFAULTTONEAREST);

        var info = new NativeMethods.MONITORINFO { cbSize = (uint)sizeof(NativeMethods.MONITORINFO) };
        if (!NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        var rect = info.rcMonitor;
        return new ScreenBounds(rect.Left, rect.Top, rect.Right, rect.Bottom);
    }
}
