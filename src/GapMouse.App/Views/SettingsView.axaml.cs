using Avalonia.Controls;
using Avalonia.Input;
using GapMouse.App.ViewModels;
using GapMouse.Application.Shell;

namespace GapMouse.App.Views;

public sealed partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        HotkeyBox.KeyDown += OnHotkeyKeyDown;
    }

    private static string? MapKey(Key key) => key switch
    {
        >= Key.A and <= Key.Z => ((char)('A' + (key - Key.A))).ToString(),
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        >= Key.F1 and <= Key.F24 => "F" + (key - Key.F1 + 1),
        _ => null,
    };

    private void OnHotkeyKeyDown(object? sender, KeyEventArgs e)
    {
        var key = MapKey(e.Key);
        if (key is null || DataContext is not SettingsViewModel vm)
        {
            return;
        }

        var modifiers = HotkeyModifiers.None;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            modifiers |= HotkeyModifiers.Control;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            modifiers |= HotkeyModifiers.Alt;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            modifiers |= HotkeyModifiers.Shift;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            modifiers |= HotkeyModifiers.Windows;
        }

        vm.CaptureHotkey(modifiers, key);
        e.Handled = true;
    }
}
