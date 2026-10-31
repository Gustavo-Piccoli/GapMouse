using GapMouse.Domain.Engine;
using GapMouse.Domain.Geometry;
using GapMouse.Infrastructure.Win32;

namespace GapMouse.Infrastructure.Tests.Win32;

/// <summary>Tests against real Win32 state. Excluded from CI; the cursor moves by 1 px and back.</summary>
[Trait("Category", "Win32")]
public class Win32AdaptersTests
{
    [Fact]
    public void GetIdleTime_Always_IsNonNegative() =>
        new Win32IdleTimeProvider().GetIdleTime().ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);

    [Fact]
    public void GetBoundsAt_CursorPosition_ContainsCursor()
    {
        var position = new Win32CursorController().GetPosition();

        var bounds = new Win32MonitorBoundsProvider().GetBoundsAt(position);

        (bounds.Left <= position.X && position.X < bounds.Right).ShouldBeTrue();
        (bounds.Top <= position.Y && position.Y < bounds.Bottom).ShouldBeTrue();
    }

    [Fact]
    public void TrySetPosition_CurrentPosition_RoundTrips()
    {
        var cursor = new Win32CursorController();
        var original = cursor.GetPosition();

        cursor.TrySetPosition(original).ShouldBeTrue();

        cursor.GetPosition().ShouldBe(original);
    }

    /// <summary>
    /// Pins the legacy assumption that SetCursorPos does not reset the system idle timer.
    /// Must be run with the mouse and keyboard untouched (~1.5 s).
    /// </summary>
    [Fact]
    public void TrySetPosition_Moving_DoesNotResetSystemIdle()
    {
        var idle = new Win32IdleTimeProvider();
        var cursor = new Win32CursorController();
        Thread.Sleep(1500);
        var idleBefore = idle.GetIdleTime();
        var original = cursor.GetPosition();

        cursor.TrySetPosition(new PixelPoint(original.X + 1, original.Y)).ShouldBeTrue();
        cursor.TrySetPosition(original).ShouldBeTrue();

        idle.GetIdleTime().ShouldBeGreaterThanOrEqualTo(idleBefore);
    }

    [Theory]
    [InlineData(PowerRequest.KeepSystemAndDisplayAwake)]
    [InlineData(PowerRequest.KeepSystemAwake)]
    [InlineData(PowerRequest.None)]
    public void Apply_AnyRequest_DoesNotThrow(PowerRequest request) =>
        Should.NotThrow(() => new Win32PowerRequestService().Apply(request));
}
