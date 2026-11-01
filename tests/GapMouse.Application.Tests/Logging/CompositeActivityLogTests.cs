using GapMouse.Application.Logging;

namespace GapMouse.Application.Tests.Logging;

public sealed class CompositeActivityLogTests
{
    private sealed class RecordingLog : IActivityLog
    {
        public List<LogEntry> Entries { get; } = [];

        public void Append(LogEntry entry) => Entries.Add(entry);
    }

    private sealed class ThrowingLog : IActivityLog
    {
        public void Append(LogEntry entry) => throw new InvalidOperationException("boom");
    }

    private static readonly LogEntry Sample = new(DateTimeOffset.UnixEpoch, LogKind.Start, "x");

    [Fact]
    public void Append_ForwardsToEverySink()
    {
        var a = new RecordingLog();
        var b = new RecordingLog();

        new CompositeActivityLog([a, b]).Append(Sample);

        a.Entries.ShouldBe([Sample]);
        b.Entries.ShouldBe([Sample]);
    }

    [Fact]
    public void Append_SinkThrows_OtherSinksStillReceive()
    {
        var a = new RecordingLog();
        var b = new RecordingLog();

        new CompositeActivityLog([a, new ThrowingLog(), b]).Append(Sample);

        a.Entries.ShouldBe([Sample]);
        b.Entries.ShouldBe([Sample]);
    }
}
