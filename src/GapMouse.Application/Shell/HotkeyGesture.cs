using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace GapMouse.Application.Shell;

/// <summary>A global hotkey: modifiers plus one key (A–Z, 0–9 or F1–F24).</summary>
public sealed record HotkeyGesture(HotkeyModifiers Modifiers, string Key)
{
    /// <summary>The Win32 virtual-key code of <see cref="Key"/>.</summary>
    public uint VirtualKey => KeyToVirtualKey(Key) ?? 0;

    public static bool TryParse(string? text, [NotNullWhen(true)] out HotkeyGesture? gesture)
    {
        gesture = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var modifiers = HotkeyModifiers.None;
        string? key = null;
        foreach (var raw in text.Split('+'))
        {
            var token = raw.Trim();
            if (token.Length == 0)
            {
                return false;
            }

            var modifier = ToModifier(token);
            if (modifier != HotkeyModifiers.None)
            {
                if ((modifiers & modifier) != 0)
                {
                    return false;
                }

                modifiers |= modifier;
                continue;
            }

            var upper = token.ToUpperInvariant();
            if (key is not null || KeyToVirtualKey(upper) is null)
            {
                return false;
            }

            key = upper;
        }

        if (key is null || (modifiers & (HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Windows)) == 0)
        {
            return false;
        }

        gesture = new HotkeyGesture(modifiers, key);
        return true;
    }

    public override string ToString()
    {
        var parts = new List<string>(5);
        if ((Modifiers & HotkeyModifiers.Control) != 0)
        {
            parts.Add("Ctrl");
        }

        if ((Modifiers & HotkeyModifiers.Alt) != 0)
        {
            parts.Add("Alt");
        }

        if ((Modifiers & HotkeyModifiers.Shift) != 0)
        {
            parts.Add("Shift");
        }

        if ((Modifiers & HotkeyModifiers.Windows) != 0)
        {
            parts.Add("Win");
        }

        parts.Add(Key);
        return string.Join('+', parts);
    }

    private static HotkeyModifiers ToModifier(string token) => token.ToUpperInvariant() switch
    {
        "CTRL" or "CONTROL" => HotkeyModifiers.Control,
        "ALT" => HotkeyModifiers.Alt,
        "SHIFT" => HotkeyModifiers.Shift,
        "WIN" or "WINDOWS" or "META" => HotkeyModifiers.Windows,
        _ => HotkeyModifiers.None,
    };

    private static uint? KeyToVirtualKey(string key)
    {
        if (key.Length == 1)
        {
            var c = key[0];
            if (c is >= 'A' and <= 'Z')
            {
                return 0x41u + (uint)(c - 'A');
            }

            if (c is >= '0' and <= '9')
            {
                return 0x30u + (uint)(c - '0');
            }

            return null;
        }

        if (key.Length is 2 or 3
            && key[0] == 'F'
            && key[1] is >= '1' and <= '9'
            && uint.TryParse(key.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var n)
            && n is >= 1 and <= 24)
        {
            return 0x70u + n - 1;
        }

        return null;
    }
}
