using GapMouse.Application.Logging;

namespace GapMouse.DevConsole;

/// <summary>Prints one colored line per activity log entry.</summary>
internal static class ConsoleEventPrinter
{
    public static void Print(LogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        ConsoleColor color = entry.Kind switch
        {
            LogKind.Start => ConsoleColor.Yellow,
            LogKind.Waiting => ConsoleColor.Cyan,
            LogKind.Moving => ConsoleColor.Green,
            LogKind.Return => ConsoleColor.Gray,
            LogKind.Paused or LogKind.Resumed => ConsoleColor.Magenta,
            LogKind.Config => ConsoleColor.DarkGray,
            LogKind.Stop => ConsoleColor.Red,
            LogKind.Warn => ConsoleColor.Yellow,
            LogKind.Error => ConsoleColor.Red,
            _ => ConsoleColor.Gray,
        };

        ConsoleOutput.WriteLine(color, entry.ToLine());
    }
}
