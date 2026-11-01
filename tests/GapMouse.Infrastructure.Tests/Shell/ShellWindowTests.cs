using System.Runtime.InteropServices;
using GapMouse.Infrastructure.Shell;

namespace GapMouse.Infrastructure.Tests.Shell;

internal static partial class ShellTestNative
{
    internal const uint WM_ENDSESSION = 0x0016;
    internal const uint WM_HOTKEY = 0x0312;
    internal const uint WM_WTSSESSION_CHANGE = 0x02B1;

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

    [Fact]
    public void ThrowingHandler_RaisesHandlerFailedAndQueryEndSessionStillReturnsOne()
    {
        using var window = new ShellWindow();
        Exception? failure = null;
        window.HandlerFailed += e => failure = e;
        window.ActivationRequested += (_, _) => throw new InvalidOperationException("boom");

        ShellTestNative.SendMessageW(window.Handle, ShellTestNative.RegisterWindowMessageW("GapMouse.Activate"), 0, 0);
        failure.ShouldBeOfType<InvalidOperationException>();

        failure = null;
        window.HandlerFailed += _ => throw new InvalidOperationException("reporter fails too");
        ShellTestNative.SendMessageW(window.Handle, ShellTestNative.RegisterWindowMessageW("GapMouse.Activate"), 0, 0);
        failure.ShouldNotBeNull();
        ShellTestNative.SendMessageW(window.Handle, 0x0011, 0, 0).ShouldBe(1);
    }

    [Fact]
    public void ShellWindow_RegistersAndUnregistersSessionNotifications()
    {
        Exception? failure = null;
        var window = new ShellWindow();
        window.HandlerFailed += e => failure = e;

        window.Dispose();

        failure.ShouldBeNull();
    }

    [Fact]
    public void QueryIsSessionLocked_ReturnsFalseInInteractiveSession()
    {
        ShellWindow.QueryIsSessionLocked().ShouldBeFalse();
    }

    [Fact]
    public void WtsSessionLock_RaisesSessionLockChangedTrue()
    {
        using var window = new ShellWindow();
        var values = new List<bool>();
        window.SessionLockChanged += (_, locked) => values.Add(locked);

        ShellTestNative.SendMessageW(window.Handle, ShellTestNative.WM_WTSSESSION_CHANGE, 0x7, 1);

        values.ShouldBe([true]);
    }

    [Fact]
    public void WtsSessionUnlock_RaisesSessionLockChangedFalse()
    {
        using var window = new ShellWindow();
        var values = new List<bool>();
        window.SessionLockChanged += (_, locked) => values.Add(locked);

        ShellTestNative.SendMessageW(window.Handle, ShellTestNative.WM_WTSSESSION_CHANGE, 0x8, 1);

        values.ShouldBe([false]);
    }

    [Fact]
    public void OtherWtsCodes_RaiseNothing()
    {
        using var window = new ShellWindow();
        var raised = 0;
        window.SessionLockChanged += (_, _) => raised++;

        foreach (var code in new nint[] { 0x1, 0x2, 0x5, 0x6, 0x9 })
        {
            ShellTestNative.SendMessageW(window.Handle, ShellTestNative.WM_WTSSESSION_CHANGE, code, 1);
        }

        raised.ShouldBe(0);
    }
}
