using GapMouse.Application.Shell;

namespace GapMouse.Application.Tests.Shell;

public sealed class HotkeyGestureTests
{
    [Fact]
    public void TryParse_CtrlAltP_Parses()
    {
        HotkeyGesture.TryParse("Ctrl+Alt+P", out var g).ShouldBeTrue();

        g!.Modifiers.ShouldBe(HotkeyModifiers.Control | HotkeyModifiers.Alt);
        g.Key.ShouldBe("P");
        g.VirtualKey.ShouldBe(0x50u);
    }

    [Fact]
    public void TryParse_LooseCaseAndSpaces_ProducesCanonicalForm()
    {
        HotkeyGesture.TryParse("control + shift + f12", out var g).ShouldBeTrue();

        g!.ToString().ShouldBe("Ctrl+Shift+F12");
        g.VirtualKey.ShouldBe(0x7Bu);
    }

    [Fact]
    public void TryParse_WinDigit_Parses()
    {
        HotkeyGesture.TryParse("Win+1", out var g).ShouldBeTrue();

        g!.VirtualKey.ShouldBe(0x31u);
        g.Modifiers.ShouldBe(HotkeyModifiers.Windows);
    }

    [Fact]
    public void TryParse_AliasesWindowsAndMeta_MapToWin()
    {
        HotkeyGesture.TryParse("Windows+A", out var a).ShouldBeTrue();
        HotkeyGesture.TryParse("Meta+A", out var b).ShouldBeTrue();

        a!.Modifiers.ShouldBe(HotkeyModifiers.Windows);
        b!.Modifiers.ShouldBe(HotkeyModifiers.Windows);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("P")]
    [InlineData("Shift+P")]
    [InlineData("Ctrl+Alt")]
    [InlineData("Ctrl+P+Q")]
    [InlineData("Ctrl+Ctrl+P")]
    [InlineData("Ctrl+F25")]
    [InlineData("Ctrl+Esc")]
    public void TryParse_Invalid_ReturnsFalse(string? text)
    {
        HotkeyGesture.TryParse(text, out var g).ShouldBeFalse();

        g.ShouldBeNull();
    }

    [Theory]
    [InlineData("Ctrl+Alt+Shift+Win+F24")]
    [InlineData("Alt+0")]
    [InlineData("Ctrl+Z")]
    public void ToString_RoundTripsThroughTryParse(string text)
    {
        HotkeyGesture.TryParse(text, out var g).ShouldBeTrue();

        g!.ToString().ShouldBe(text);
        HotkeyGesture.TryParse(g.ToString(), out var again).ShouldBeTrue();
        again.ShouldBe(g);
    }
}
