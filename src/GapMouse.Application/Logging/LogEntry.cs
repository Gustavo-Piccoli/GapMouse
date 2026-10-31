using System.Globalization;

namespace GapMouse.Application.Logging;

/// <summary>One line of the activity log; <see cref="At"/> is local time.</summary>
public sealed record LogEntry(DateTimeOffset At, LogKind Kind, string Message)
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
        _ => throw new ArgumentOutOfRangeException(nameof(Kind), Kind, null),
    };

    public string ToLine() =>
        string.Create(CultureInfo.InvariantCulture, $"{At.ToString("HH:mm:ss", CultureInfo.InvariantCulture)} | {Tag} | {Message}");
}
