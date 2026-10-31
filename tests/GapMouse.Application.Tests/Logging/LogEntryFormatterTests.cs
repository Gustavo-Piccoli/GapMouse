using GapMouse.Application.Logging;
using GapMouse.Domain.Engine;
using GapMouse.Domain.Pausing;
using GapMouse.Domain.Settings;

namespace GapMouse.Application.Tests.Logging;

public sealed class LogEntryFormatterTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 31, 9, 5, 3, TimeSpan.Zero);

    [Fact]
    public void FromEvent_EngineStarted_FormatsStart()
    {
        var entry = LogEntryFormatter.FromEvent(new EngineStarted(At));

        Assert.Equal(LogKind.Start, entry.Kind);
        Assert.Equal("GapMouse started", entry.Message);
        Assert.Equal(At, entry.At);
    }

    [Fact]
    public void FromEvent_WaitingStarted_FormatsWaiting()
    {
        var entry = LogEntryFormatter.FromEvent(new WaitingStarted(At, TimeSpan.FromSeconds(120)));

        Assert.Equal(LogKind.Waiting, entry.Kind);
        Assert.Equal("Waiting for 120 s of inactivity", entry.Message);
    }

    [Fact]
    public void FromEvent_MovingStarted_FormatsMoving()
    {
        var entry = LogEntryFormatter.FromEvent(new MovingStarted(At, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10.5)));

        Assert.Equal(LogKind.Moving, entry.Kind);
        Assert.Equal("Moving the mouse every 1–10.5 s", entry.Message);
    }

    [Fact]
    public void FromEvent_UserReturned_FormatsReturn()
    {
        var entry = LogEntryFormatter.FromEvent(new UserReturned(At));

        Assert.Equal(LogKind.Return, entry.Kind);
        Assert.Equal("User activity detected", entry.Message);
    }

    [Fact]
    public void FromEvent_TimedPause_FormatsPausedUntil()
    {
        var entry = LogEntryFormatter.FromEvent(new EnginePaused(At, PauseDuration.TenMinutes, At.AddMinutes(10)));

        Assert.Equal(LogKind.Paused, entry.Kind);
        Assert.Equal("Paused until 09:15:03", entry.Message);
    }

    [Fact]
    public void FromEvent_IndefinitePause_FormatsPausedIndefinitely()
    {
        var entry = LogEntryFormatter.FromEvent(new EnginePaused(At, PauseDuration.Indefinite, null));

        Assert.Equal(LogKind.Paused, entry.Kind);
        Assert.Equal("Paused indefinitely", entry.Message);
    }

    [Fact]
    public void FromEvent_ManualResume_FormatsResumed()
    {
        var entry = LogEntryFormatter.FromEvent(new EngineResumed(At, ResumeReason.Manual));

        Assert.Equal(LogKind.Resumed, entry.Kind);
        Assert.Equal("Resumed (manual)", entry.Message);
    }

    [Fact]
    public void FromEvent_ExpiredResume_FormatsResumed()
    {
        var entry = LogEntryFormatter.FromEvent(new EngineResumed(At, ResumeReason.PauseExpired));

        Assert.Equal(LogKind.Resumed, entry.Kind);
        Assert.Equal("Resumed (pause expired)", entry.Message);
    }

    [Fact]
    public void FromEvent_SettingsChanged_FormatsConfig()
    {
        var entry = LogEntryFormatter.FromEvent(new SettingsChanged(At, EngineSettings.Default));

        Assert.Equal(LogKind.Config, entry.Kind);
        Assert.Equal("Settings changed", entry.Message);
    }

    [Fact]
    public void FromEvent_EngineStopped_FormatsStop()
    {
        var entry = LogEntryFormatter.FromEvent(new EngineStopped(At));

        Assert.Equal(LogKind.Stop, entry.Kind);
        Assert.Equal("GapMouse stopped", entry.Message);
    }

    [Fact]
    public void ToLine_UsesPipeFormat()
    {
        var entry = new LogEntry(At, LogKind.Moving, "x");

        Assert.Equal("09:05:03 | MOVING | x", entry.ToLine());
    }

    [Fact]
    public void FromEvent_UnknownEventType_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LogEntryFormatter.FromEvent(new TestEvent(At)));
    }

    private sealed record TestEvent(DateTimeOffset At) : EngineEvent(At);
}
