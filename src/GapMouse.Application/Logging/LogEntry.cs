using System.Globalization;
using GapMouse.Domain.Engine;

namespace GapMouse.Application.Logging;

/// <summary>One line of the activity log; <see cref="At"/> is local time.</summary>
public sealed record LogEntry(DateTimeOffset At, LogKind Kind, string Message, EngineEvent? Source = null)
{
    public string Tag => Kind switch
    {
        LogKind.Start => "START",
        LogKind.Waiting => "WAITING",
        LogKind.Moving => "MOVING",
        LogKind.Return => "RETURN",
        LogKind.Paused => "PAUSED",
        LogKind.Resumed => "RESUMED",
        LogKind.Config => "CONFIG",
        LogKind.Stop => "STOP",
        LogKind.Warn => "WARN",
        LogKind.Error => "ERROR",
        LogKind.Locked => "LOCKED",
        LogKind.Unlocked => "UNLOCKED",
        _ => throw new ArgumentOutOfRangeException(nameof(Kind), Kind, null),
    };

    public string ToLine() =>
        string.Create(CultureInfo.InvariantCulture, $"{At.ToString("HH:mm:ss", CultureInfo.InvariantCulture)} | {Tag} | {Message}");
}
