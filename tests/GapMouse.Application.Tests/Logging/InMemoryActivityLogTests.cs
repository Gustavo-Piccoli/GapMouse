using GapMouse.Application.Logging;

namespace GapMouse.Application.Tests.Logging;

public sealed class InMemoryActivityLogTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 31, 9, 0, 0, TimeSpan.Zero);

    private static LogEntry Entry(int n) => new(At, LogKind.Config, $"entry {n}");

    [Fact]
    public void Append_KeepsOldestFirst()
    {
        var log = new InMemoryActivityLog();

        log.Append(Entry(1));
        log.Append(Entry(2));

        log.GetEntries().Select(e => e.Message).ShouldBe(["entry 1", "entry 2"]);
    }

    [Fact]
    public void Append_OverCapacity_DropsOldest()
    {
        var log = new InMemoryActivityLog();

        for (var i = 1; i <= 1005; i++)
        {
            log.Append(Entry(i));
        }

        var entries = log.GetEntries();
        entries.Count.ShouldBe(InMemoryActivityLog.Capacity);
        entries[0].Message.ShouldBe("entry 6");
        entries[^1].Message.ShouldBe("entry 1005");
    }

    [Fact]
    public void Append_RaisesEntryAdded()
    {
        var log = new InMemoryActivityLog();
        List<LogEntry> raised = [];
        log.EntryAdded += (_, e) => raised.Add(e);
        var entry = Entry(1);

        log.Append(entry);

        raised.ShouldBe([entry]);
    }

    [Fact]
    public void Append_FromManyThreads_KeepsAllUpToCapacity()
    {
        var log = new InMemoryActivityLog();

        Parallel.For(0, 500, i => log.Append(Entry(i)));

        log.GetEntries().Count.ShouldBe(500);
    }
}
