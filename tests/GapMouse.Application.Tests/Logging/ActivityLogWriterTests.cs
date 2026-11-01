using GapMouse.Application.Logging;
using GapMouse.Domain.Engine;
using GapMouse.Testing.Engine;

namespace GapMouse.Application.Tests.Logging;

public sealed class ActivityLogWriterTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 31, 9, 5, 3, TimeSpan.Zero);

    [Fact]
    public void EngineEvent_AppendsFormattedEntry()
    {
        var engine = new FakeEngineController();
        var log = new RecordingLog();
        using var writer = new ActivityLogWriter(engine, log);

        engine.Raise(new UserReturned(At));

        var entry = Assert.Single(log.Entries);
        Assert.Equal(LogKind.Return, entry.Kind);
        Assert.Equal("User activity detected", entry.Message);
    }

    [Fact]
    public void Dispose_Unsubscribes()
    {
        var engine = new FakeEngineController();
        var log = new RecordingLog();
        var writer = new ActivityLogWriter(engine, log);

        writer.Dispose();
        engine.Raise(new UserReturned(At));

        Assert.Empty(log.Entries);
    }

    private sealed class RecordingLog : IActivityLog
    {
        public List<LogEntry> Entries { get; } = [];

        public void Append(LogEntry entry) => Entries.Add(entry);
    }
}
