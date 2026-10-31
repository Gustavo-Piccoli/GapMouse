using System.Globalization;
using GapMouse.Domain.Engine;

namespace GapMouse.DevConsole;

/// <summary>Prints one line per engine event, in the legacy script format and colors.</summary>
internal static class ConsoleEventPrinter
{
    public static void Print(EngineEvent engineEvent)
    {
        ArgumentNullException.ThrowIfNull(engineEvent);

        (ConsoleColor Color, string Text) line = engineEvent switch
        {
            EngineStarted => (ConsoleColor.Yellow, "[START]   | Engine started (p = pause, t = pause 1 min, r = resume, q = quit)"),
            WaitingStarted w => (ConsoleColor.Cyan, $"[WAITING] | Waiting for {Seconds(w.IdleThreshold)} s of inactivity"),
            MovingStarted m => (ConsoleColor.Green, $"[MOVING]  | Moving the mouse every {Seconds(m.IntervalMin)}–{Seconds(m.IntervalMax)} s"),
            UserReturned => (ConsoleColor.Gray, "[RETURN]  | User activity detected"),
            EnginePaused p => (
                ConsoleColor.Magenta,
                $"[PAUSED]  | Paused ({(p.ResumesAt is { } until ? "until " + until.ToString("HH:mm:ss", CultureInfo.InvariantCulture) : "indefinitely")})"),
            EngineResumed r => (ConsoleColor.Magenta, $"[RESUMED] | Resumed ({(r.Reason == ResumeReason.Manual ? "manual" : "pause expired")})"),
            SettingsChanged => (ConsoleColor.DarkGray, "[CONFIG]  | Settings changed"),
            EngineStopped => (ConsoleColor.Red, "[STOP]    | Engine stopped"),
            _ => (ConsoleColor.DarkGray, "[EVENT]   | " + engineEvent.GetType().Name),
        };

        ConsoleOutput.WriteLine(line.Color, $"[{engineEvent.At.ToString("HH:mm:ss", CultureInfo.InvariantCulture)}] | {line.Text}");
    }

    private static string Seconds(TimeSpan value) => value.TotalSeconds.ToString("0.##", CultureInfo.InvariantCulture);
}
