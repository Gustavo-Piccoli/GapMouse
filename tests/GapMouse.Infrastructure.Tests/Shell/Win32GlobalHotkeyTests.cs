using GapMouse.Application.Shell;
using GapMouse.Infrastructure.Shell;

namespace GapMouse.Infrastructure.Tests.Shell;

[Collection("ShellWin32")]
[Trait("Category", "Win32")]
public sealed class Win32GlobalHotkeyTests
{
    private static HotkeyGesture Gesture()
    {
        HotkeyGesture.TryParse("Ctrl+Alt+Shift+F24", out var gesture).ShouldBeTrue();
        return gesture!;
    }

    [Fact]
    public void TryRegister_FreeGesture_ReturnsTrue()
    {
        using var window = new ShellWindow();
        using var hotkey = new Win32GlobalHotkey(window);

        hotkey.TryRegister(Gesture()).ShouldBeTrue();
    }

    [Fact]
    public void TryRegister_SameGestureInSecondWindow_ReturnsFalse()
    {
        using var first = new ShellWindow();
        using var second = new ShellWindow();
        using var hotkey1 = new Win32GlobalHotkey(first);
        using var hotkey2 = new Win32GlobalHotkey(second);

        hotkey1.TryRegister(Gesture()).ShouldBeTrue();
        hotkey2.TryRegister(Gesture()).ShouldBeFalse();
    }

    [Fact]
    public void HotkeyMessage_RaisesPressed()
    {
        using var window = new ShellWindow();
        using var hotkey = new Win32GlobalHotkey(window);
        var raised = 0;
        hotkey.Pressed += (_, _) => raised++;

        ShellTestNative.SendMessageW(window.Handle, ShellTestNative.WM_HOTKEY, 1, 0);

        raised.ShouldBe(1);
    }
}
