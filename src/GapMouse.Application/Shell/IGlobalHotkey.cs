namespace GapMouse.Application.Shell;

/// <summary>A system-wide hotkey registration.</summary>
public interface IGlobalHotkey : IDisposable
{
    /// <summary>Registers the gesture, replacing any previous registration. False if the system refuses it.</summary>
    bool TryRegister(HotkeyGesture gesture);

    void Unregister();

    event EventHandler? Pressed;
}
