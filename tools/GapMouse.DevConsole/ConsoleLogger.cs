using Microsoft.Extensions.Logging;

namespace GapMouse.DevConsole;

/// <summary>Minimal logger that prints Warning and above in yellow.</summary>
internal sealed class ConsoleLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        if (!IsEnabled(logLevel))
        {
            return;
        }

        ConsoleOutput.WriteLine(ConsoleColor.Yellow, $"[{logLevel.ToString().ToUpperInvariant()}] | {formatter(state, exception)}");
        if (exception is not null)
        {
            ConsoleOutput.WriteLine(ConsoleColor.Yellow, exception.ToString());
        }
    }
}
