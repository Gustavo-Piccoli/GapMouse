using System.Runtime.InteropServices;
using static GapMouse.Infrastructure.Shell.ShellNativeMethods;

namespace GapMouse.Infrastructure.Shell;

/// <summary>
/// A hidden top-level tool window that receives tray, hotkey, activation and session messages.
/// Create it on a thread that pumps messages.
/// </summary>
public sealed unsafe class ShellWindow : IDisposable
{
    public const string ClassName = "GapMouse.ShellWindow";

    private static readonly Lock ClassLock = new();
    private static bool s_classRegistered;

    private GCHandle _self;
    private bool _sessionNotificationsRegistered;
    private Exception? _registrationFailure;

    public ShellWindow()
    {
        EnsureClassRegistered();
        _self = GCHandle.Alloc(this);
        Handle = CreateWindowExW(
            WS_EX_TOOLWINDOW, ClassName, "GapMouse", WS_POPUP, 0, 0, 0, 0, 0, 0, GetModuleHandleW(0), GCHandle.ToIntPtr(_self));
        if (Handle == 0)
        {
            var error = Marshal.GetLastPInvokeError();
            _self.Free();
            throw new InvalidOperationException($"Could not create the shell window (error {error}).");
        }

        if (WTSRegisterSessionNotification(Handle, NOTIFY_FOR_THIS_SESSION))
        {
            _sessionNotificationsRegistered = true;
        }
        else
        {
            _registrationFailure = new InvalidOperationException(
                $"Could not register for session notifications (error {Marshal.GetLastPInvokeError()}).");
        }
    }

    public nint Handle { get; private set; }

    /// <summary>Raised when a second launch asks this instance to show its window.</summary>
    public event EventHandler? ActivationRequested;

    /// <summary>Raised synchronously when Windows is logging off or shutting down.</summary>
    public event EventHandler? SessionEnding;

    /// <summary>Raised when the session is locked (true) or unlocked (false).</summary>
    public event EventHandler<bool>? SessionLockChanged;

    /// <summary>Raised when an event handler or message observer threw; the exception is otherwise contained.</summary>
    public event Action<Exception>? HandlerFailed;

    /// <summary>
    /// Returns whether the current session is locked; false when it cannot be determined.
    /// </summary>
    public static bool QueryIsSessionLocked()
    {
        nint buffer = 0;
        try
        {
            if (!WTSQuerySessionInformationW(0, WTS_CURRENT_SESSION, WTSSessionInfoEx, out buffer, out var bytes)
                || buffer == 0
                || bytes < 20)
            {
                return false;
            }

            // WTSINFOEXW: Level (int) at 0, then the 8-aligned level-1 data at 8:
            // SessionId at 8, SessionState at 12, SessionFlags at 16.
            if (Marshal.ReadInt32(buffer, 0) != 1)
            {
                return false;
            }

            return Marshal.ReadInt32(buffer, 16) == WTS_SESSIONSTATE_LOCK;
        }
#pragma warning disable CA1031 // Any failure means "not known to be locked".
        catch (Exception)
#pragma warning restore CA1031
        {
            return false;
        }
        finally
        {
            if (buffer != 0)
            {
                WTSFreeMemory(buffer);
            }
        }
    }

    internal static uint ActivateMessage { get; } = RegisterWindowMessageW("GapMouse.Activate");

    internal static uint TaskbarCreatedMessage { get; } = RegisterWindowMessageW("TaskbarCreated");

    /// <summary>Every message received by the window, for the tray icon and hotkey.</summary>
    internal event Action<uint, nint, nint>? MessageReceived;

    public void Dispose()
    {
        var handle = Handle;
        if (handle == 0)
        {
            return;
        }

        Handle = 0;
        if (_sessionNotificationsRegistered)
        {
            _sessionNotificationsRegistered = false;
            WTSUnRegisterSessionNotification(handle);
        }

        DestroyWindow(handle);
        if (_self.IsAllocated)
        {
            _self.Free();
        }
    }

    private static void EnsureClassRegistered()
    {
        lock (ClassLock)
        {
            if (s_classRegistered)
            {
                return;
            }

            var wc = new WNDCLASSEXW
            {
                cbSize = (uint)sizeof(WNDCLASSEXW),
                lpfnWndProc = (nint)(delegate* unmanaged<nint, uint, nint, nint, nint>)&WndProc,
                hInstance = GetModuleHandleW(0),
                lpszClassName = Marshal.StringToHGlobalUni(ClassName),
            };
            if (RegisterClassExW(in wc) == 0)
            {
                throw new InvalidOperationException($"Could not register the shell window class (error {Marshal.GetLastPInvokeError()}).");
            }

            s_classRegistered = true;
        }
    }

    [UnmanagedCallersOnly]
    private static nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == WM_NCCREATE)
        {
            SetWindowLongPtrW(hwnd, GWLP_USERDATA, *(nint*)lParam);
            return DefWindowProcW(hwnd, msg, wParam, lParam);
        }

        var data = GetWindowLongPtrW(hwnd, GWLP_USERDATA);
        if (msg == WM_NCDESTROY)
        {
            SetWindowLongPtrW(hwnd, GWLP_USERDATA, 0);
        }

        if (data != 0 && GCHandle.FromIntPtr(data).Target is ShellWindow window)
        {
            try
            {
                if (window.HandleMessage(msg, wParam, lParam, out var result))
                {
                    return result;
                }
            }
#pragma warning disable CA1031 // An exception must never cross the unmanaged callback boundary.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                window.ReportFailure(ex);
                if (msg == WM_QUERYENDSESSION)
                {
                    return 1;
                }
            }
        }

        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    private void ReportFailure(Exception exception)
    {
        try
        {
            HandlerFailed?.Invoke(exception);
        }
#pragma warning disable CA1031 // The failure reporter itself must not cross the unmanaged callback boundary.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }

    private bool HandleMessage(uint msg, nint wParam, nint lParam, out nint result)
    {
        MessageReceived?.Invoke(msg, wParam, lParam);
        result = 0;
        if (_registrationFailure is { } failure)
        {
            _registrationFailure = null;
            ReportFailure(failure);
        }

        if (msg == WM_WTSSESSION_CHANGE)
        {
            if (wParam == WTS_SESSION_LOCK)
            {
                SessionLockChanged?.Invoke(this, true);
            }
            else if (wParam == WTS_SESSION_UNLOCK)
            {
                SessionLockChanged?.Invoke(this, false);
            }

            return true;
        }

        if (msg == WM_QUERYENDSESSION)
        {
            result = 1;
            return true;
        }

        if (msg == WM_ENDSESSION)
        {
            if (wParam != 0)
            {
                SessionEnding?.Invoke(this, EventArgs.Empty);
            }

            return true;
        }

        if (msg == ActivateMessage)
        {
            ActivationRequested?.Invoke(this, EventArgs.Empty);
            return true;
        }

        return false;
    }
}
