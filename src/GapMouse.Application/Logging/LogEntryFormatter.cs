using System.Globalization;
using GapMouse.Domain.Engine;

namespace GapMouse.Application.Logging;

/// <summary>Turns engine events into English log entries.</summary>
public static class LogEntryFormatter
{
    public static LogEntry FromEvent(EngineEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);
        return e switch
        {
            EngineStarted => new LogEntry(e.At, LogKind.Start, "GapMouse started"),
            WaitingStarted w => new LogEntry(e.At, LogKind.Waiting, $"Waiting for {Seconds(w.IdleThreshold)} s of inactivity"),
            MovingStarted m => new LogEntry(e.At, LogKind.Moving, $"Moving the mouse every {Seconds(m.IntervalMin)}–{Seconds(m.IntervalMax)} s"),
            UserReturned => new LogEntry(e.At, LogKind.Return, "User activity detected"),
            EnginePaused { ResumesAt: { } until } => new LogEntry(e.At, LogKind.Paused, $"Paused until {until.ToString("HH:mm:ss", CultureInfo.InvariantCulture)}"),
            EnginePaused => new LogEntry(e.At, LogKind.Paused, "Paused indefinitely"),
            EngineResumed { Reason: ResumeReason.Manual } => new LogEntry(e.At, LogKind.Resumed, "Resumed (manual)"),
            EngineResumed => new LogEntry(e.At, LogKind.Resumed, "Resumed (pause expired)"),
            SettingsChanged => new LogEntry(e.At, LogKind.Config, "Settings changed"),
            EngineStopped => new LogEntry(e.At, LogKind.Stop, "GapMouse stopped"),
            _ => throw new ArgumentOutOfRangeException(nameof(e), e.GetType().Name, "Unknown engine event."),
        };
    }

    private static string Seconds(TimeSpan value) =>
        value.TotalSeconds.ToString("0.##", CultureInfo.InvariantCulture);
}
