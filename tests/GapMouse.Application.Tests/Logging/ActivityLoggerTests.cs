using GapMouse.Application.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace GapMouse.Application.Tests.Logging;

public sealed class ActivityLoggerTests
{
    private readonly RecordingLog _log = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void LogWarning_AppendsWarnEntry()
    {
        var logger = new ActivityLogger<ActivityLoggerTests>(_log, _time);

        Write(logger, LogLevel.Warning, "careful now");

        var entry = Assert.Single(_log.Entries);
        Assert.Equal(LogKind.Warn, entry.Kind);
        Assert.Equal("careful now", entry.Message);
        Assert.Equal(_time.GetLocalNow(), entry.At);
    }

    [Fact]
    public void LogError_WithException_AppendsErrorWithExceptionMessage()
    {
        var logger = new ActivityLogger<ActivityLoggerTests>(_log, _time);

        Write(logger, LogLevel.Error, "failed", new InvalidOperationException("boom"));

        var entry = Assert.Single(_log.Entries);
        Assert.Equal(LogKind.Error, entry.Kind);
        Assert.Equal("failed: boom", entry.Message);
    }

    [Fact]
    public void LogInformation_DoesNotAppend()
    {
        var logger = new ActivityLogger<ActivityLoggerTests>(_log, _time);

        Write(logger, LogLevel.Information, "hello");

        Assert.Empty(_log.Entries);
    }

    [Fact]
    public void AnyLevel_ForwardsToInner()
    {
        var inner = new CountingLogger();
        var logger = new ActivityLogger<ActivityLoggerTests>(_log, _time, inner);

        Write(logger, LogLevel.Information, "a");
        Write(logger, LogLevel.Warning, "b");
        Write(logger, LogLevel.Critical, "c");

        Assert.Equal(3, inner.Count);
        Assert.Equal(2, _log.Entries.Count);
    }

    private static void Write(ActivityLogger<ActivityLoggerTests> logger, LogLevel level, string message, Exception? exception = null) =>
        logger.Log(level, 0, message, exception, (m, _) => m);

    private sealed class RecordingLog : IActivityLog
    {
        public List<LogEntry> Entries { get; } = [];

        public void Append(LogEntry entry) => Entries.Add(entry);
    }

    private sealed class CountingLogger : ILogger<ActivityLoggerTests>
    {
        public int Count { get; private set; }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Count++;
    }
}
