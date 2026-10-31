using System.ComponentModel;
using System.Runtime.InteropServices;
using GapMouse.Application.Abstractions;
using GapMouse.Domain.Engine;

namespace GapMouse.Infrastructure.Win32;

/// <remarks>Execution state is per-thread, so call <see cref="Apply"/> from a single thread.</remarks>
public sealed class Win32PowerRequestService : IPowerRequestService
{
    public void Apply(PowerRequest request)
    {
        var flags = request switch
        {
            PowerRequest.None => NativeMethods.ES_CONTINUOUS,
            PowerRequest.KeepSystemAwake => NativeMethods.ES_CONTINUOUS | NativeMethods.ES_SYSTEM_REQUIRED,
            PowerRequest.KeepSystemAndDisplayAwake => NativeMethods.ES_CONTINUOUS
                | NativeMethods.ES_SYSTEM_REQUIRED
                | NativeMethods.ES_DISPLAY_REQUIRED,
            _ => throw new ArgumentOutOfRangeException(nameof(request), request, null),
        };

        if (NativeMethods.SetThreadExecutionState(flags) == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }
}
