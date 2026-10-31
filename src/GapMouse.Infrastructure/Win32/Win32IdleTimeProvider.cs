using System.ComponentModel;
using System.Runtime.InteropServices;
using GapMouse.Application.Abstractions;

namespace GapMouse.Infrastructure.Win32;

public sealed class Win32IdleTimeProvider : IIdleTimeProvider
{
    public unsafe TimeSpan GetIdleTime()
    {
        var info = new NativeMethods.LASTINPUTINFO { cbSize = (uint)sizeof(NativeMethods.LASTINPUTINFO) };
        if (!NativeMethods.GetLastInputInfo(ref info))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return IdleTime.FromTicks((uint)Environment.TickCount, info.dwTime);
    }
}
