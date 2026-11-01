using GapMouse.Application.Shell;
using static GapMouse.Infrastructure.Shell.ShellNativeMethods;

namespace GapMouse.Infrastructure.Shell;

/// <summary>
/// A system-wide hotkey delivered to a <see cref="ShellWindow"/>.
/// Contract: <see cref="TryRegister"/> always releases the previous registration first, so after a failed call no hotkey is registered.
/// </summary>
public sealed class Win32GlobalHotkey : IGlobalHotkey
{
    private const int HotkeyId = 1;

    private readonly ShellWindow _window;
    private bool _registered;
    private bool _disposed;

    public Win32GlobalHotkey(ShellWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        _window = window;
        _window.MessageReceived += OnMessage;
    }

    public event EventHandler? Pressed;

    public bool TryRegister(HotkeyGesture gesture)
    {
        ArgumentNullException.ThrowIfNull(gesture);
        ObjectDisposedException.ThrowIf(_disposed, this);
        Unregister();
        _registered = RegisterHotKey(_window.Handle, HotkeyId, (uint)gesture.Modifiers | MOD_NOREPEAT, gesture.VirtualKey);
        return _registered;
    }

    public void Unregister()
    {
        if (_registered)
        {
            UnregisterHotKey(_window.Handle, HotkeyId);
            _registered = false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Unregister();
        _window.MessageReceived -= OnMessage;
        _disposed = true;
    }

    private void OnMessage(uint msg, nint wParam, nint lParam)
    {
        if (msg == WM_HOTKEY && wParam == HotkeyId)
        {
            Pressed?.Invoke(this, EventArgs.Empty);
        }
    }
}
