using Microsoft.Extensions.Logging;

namespace GapMouse.Application.Logging;

/// <summary>Forwards to an inner logger and mirrors warnings and errors into the activity log.</summary>
public sealed class ActivityLogger<T>(IActivityLog log, TimeProvider time, ILogger<T>? inner = null) : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => inner?.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning || (inner?.IsEnabled(logLevel) ?? false);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        inner?.Log(logLevel, eventId, state, exception, formatter);

        LogKind kind;
        switch (logLevel)
        {
            case LogLevel.Warning:
                kind = LogKind.Warn;
                break;
            case LogLevel.Error:
            case LogLevel.Critical:
                kind = LogKind.Error;
                break;
            default:
                return;
        }

        var message = formatter(state, exception);
        if (exception is not null)
        {
            message += ": " + exception.Message;
        }

        log.Append(new LogEntry(time.GetLocalNow(), kind, message));
    }
}
