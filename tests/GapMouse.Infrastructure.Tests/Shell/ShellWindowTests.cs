using System.Runtime.InteropServices;
using GapMouse.Infrastructure.Shell;

namespace GapMouse.Infrastructure.Tests.Shell;

internal static partial class ShellTestNative
{
    internal const uint WM_ENDSESSION = 0x0016;
    internal const uint WM_HOTKEY = 0x0312;

    [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
    internal static partial nint SendMessageW(nint hWnd, uint msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial uint RegisterWindowMessageW(string name);

    [LibraryImport("user32.dll", EntryPoint = "FindWindowExW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint FindWindowExW(nint parent, nint after, string? cls, string? title);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindow(nint hWnd);

    internal static bool ClassWindowExists(nint handle)
    {
        nint current = 0;
        while ((current = FindWindowExW(0, current, ShellWindow.ClassName, null)) != 0)
        {
            if (current == handle)
            {
                return true;
            }
        }

        return false;
    }
}

[Collection("ShellWin32")]
[Trait("Category", "Win32")]
public sealed class ShellWindowTests
{
    [Fact]
    public void Create_HasHandleAndIsFindableByClass()
    {
        using var window = new ShellWindow();

        window.Handle.ShouldNotBe(0);
        ShellTestNative.ClassWindowExists(window.Handle).ShouldBeTrue();
    }

    [Fact]
    public void ActivateMessage_RaisesActivationRequested()
    {
        using var window = new ShellWindow();
        var raised = 0;
        window.ActivationRequested += (_, _) => raised++;

        ShellTestNative.SendMessageW(window.Handle, ShellTestNative.RegisterWindowMessageW("GapMouse.Activate"), 0, 0);

        raised.ShouldBe(1);
    }

    [Fact]
    public void EndSession_RaisesSessionEnding()
    {
        using var window = new ShellWindow();
        var raised = 0;
        window.SessionEnding += (_, _) => raised++;

        ShellTestNative.SendMessageW(window.Handle, ShellTestNative.WM_ENDSESSION, 0, 0);
        raised.ShouldBe(0);
        ShellTestNative.SendMessageW(window.Handle, ShellTestNative.WM_ENDSESSION, 1, 0);

        raised.ShouldBe(1);
    }

    [Fact]
    public void Dispose_DestroysWindow()
    {
        var window = new ShellWindow();
        var handle = window.Handle;

        window.Dispose();

        ShellTestNative.IsWindow(handle).ShouldBeFalse();
    }
}
