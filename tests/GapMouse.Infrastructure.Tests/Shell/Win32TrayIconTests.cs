using GapMouse.Application.Shell;
using GapMouse.Infrastructure.Shell;

namespace GapMouse.Infrastructure.Tests.Shell;

[Collection("ShellWin32")]
[Trait("Category", "Win32")]
public sealed class Win32TrayIconTests
{
    private static Dictionary<TrayIconState, byte[]> Icons()
    {
        var bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Shell", "tray-waiting.ico"));
        return new()
        {
            [TrayIconState.Waiting] = bytes,
            [TrayIconState.Moving] = bytes,
            [TrayIconState.Paused] = bytes,
        };
    }

    [Fact]
    public void SetState_AddsIconThenUpdates_DoesNotThrow()
    {
        using var window = new ShellWindow();
        using var tray = new Win32TrayIcon(window, Icons());

        tray.SetState(TrayIconState.Waiting, "GapMouse test");
        tray.SetState(TrayIconState.Moving, "GapMouse test 2");

        tray.IsAdded.ShouldBeTrue();
    }

    [Fact]
    public void TaskbarCreated_ReaddsIcon()
    {
        using var window = new ShellWindow();
        using var tray = new Win32TrayIcon(window, Icons());
        tray.SetState(TrayIconState.Waiting, "GapMouse test");

        ShellTestNative.SendMessageW(window.Handle, ShellTestNative.RegisterWindowMessageW("TaskbarCreated"), 0, 0);

        tray.ReaddCount.ShouldBe(1);
    }

    [Fact]
    public void Dispose_RemovesIcon()
    {
        using var window = new ShellWindow();
        var tray = new Win32TrayIcon(window, Icons());
        try
        {
            tray.SetState(TrayIconState.Paused, "GapMouse test");

            tray.Dispose();

            tray.IsAdded.ShouldBeFalse();
        }
        finally
        {
            tray.Dispose();
        }
    }

    [Fact]
    public void TaskbarCreated_AfterFailedFirstAdd_AddsIcon()
    {
        using var window = new ShellWindow();
        using var tray = new Win32TrayIcon(window, Icons());
        tray.FailNextAdd = true;
        tray.SetState(TrayIconState.Waiting, "GapMouse test");
        tray.IsAdded.ShouldBeFalse();

        ShellTestNative.SendMessageW(window.Handle, ShellTestNative.RegisterWindowMessageW("TaskbarCreated"), 0, 0);

        tray.IsAdded.ShouldBeTrue();
        tray.ReaddCount.ShouldBe(1);
    }
}
