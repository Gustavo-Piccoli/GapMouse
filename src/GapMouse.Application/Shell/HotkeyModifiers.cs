namespace GapMouse.Application.Shell;

/// <summary>Hotkey modifier keys; values equal the Win32 <c>MOD_*</c> constants.</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8,
}
