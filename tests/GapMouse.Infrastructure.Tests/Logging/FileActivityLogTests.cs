using GapMouse.Application.Logging;
using GapMouse.Infrastructure.Logging;
using GapMouse.Testing.IO;
using Microsoft.Extensions.Time.Testing;

namespace GapMouse.Infrastructure.Tests.Logging;

public sealed class FileActivityLogTests : IDisposable
{
    private readonly TempDirectory _dir = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero));

    public FileActivityLogTests() => _time.SetLocalTimeZone(TimeZoneInfo.Utc);

    public void Dispose() => _dir.Dispose();

    private string Logs => Path.Combine(_dir.Path, "logs");

    private static LogEntry EntryAt(int day, int hour = 12) =>
        new(new DateTimeOffset(2026, 10, day, hour, 0, 0, TimeSpan.Zero), LogKind.Start, "x");

    [Fact]
    public void Append_WritesLineToDailyFile()
    {
        var log = new FileActivityLog(Logs, _time);
        var entry = EntryAt(31);

        log.Append(entry);
        var path = Path.Combine(Logs, "gapmouse-2026-10-31.log");
        Assert.Equal(entry.ToLine() + Environment.NewLine, File.ReadAllText(path));

        log.Append(entry);
        Assert.Equal(2, File.ReadAllLines(path).Length);
    }

    [Fact]
    public void Append_EntryOnNextDay_GoesToNewFile()
    {
        var log = new FileActivityLog(Logs, _time);

        log.Append(EntryAt(31));
        _time.Advance(TimeSpan.FromDays(1));
        log.Append(new LogEntry(new DateTimeOffset(2026, 11, 1, 1, 0, 0, TimeSpan.Zero), LogKind.Stop, "y"));

        Assert.True(File.Exists(Path.Combine(Logs, "gapmouse-2026-10-31.log")));
        Assert.True(File.Exists(Path.Combine(Logs, "gapmouse-2026-11-01.log")));
    }

    [Fact]
    public void Configure_Disabled_WritesNothing()
    {
        var log = new FileActivityLog(Logs, _time);
        log.Configure(false, 7);

        log.Append(EntryAt(31));

        Assert.False(Directory.Exists(Logs));
    }

    [Fact]
    public void Retention_DeletesOnlyOldGapMouseLogs()
    {
        Directory.CreateDirectory(Logs);
        string[] names = ["gapmouse-2026-10-20.log", "gapmouse-2026-10-25.log", "notes.txt", "gapmouse-backup.log", "other-2026-01-01.log"];
        foreach (var name in names)
        {
            File.WriteAllText(Path.Combine(Logs, name), "x");
        }

        var log = new FileActivityLog(Logs, _time);
        log.Configure(true, 7);

        Assert.False(File.Exists(Path.Combine(Logs, "gapmouse-2026-10-20.log")));
        foreach (var name in names.Skip(1))
        {
            Assert.True(File.Exists(Path.Combine(Logs, name)), name);
        }
    }

    [Fact]
    public void Retention_OneLockedOldFile_StillDeletesOtherOldFiles()
    {
        Directory.CreateDirectory(Logs);
        var locked = Path.Combine(Logs, "gapmouse-2026-10-10.log");
        var other = Path.Combine(Logs, "gapmouse-2026-10-12.log");
        File.WriteAllText(locked, "x");
        File.WriteAllText(other, "x");
        var log = new FileActivityLog(Logs, _time);

        using (new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            log.Configure(true, 7);
        }

        Assert.True(File.Exists(locked));
        Assert.False(File.Exists(other));
    }

    [Fact]
    public void Retention_RunsAgainOnFirstAppendOfNewDay()
    {
        Directory.CreateDirectory(Logs);
        var log = new FileActivityLog(Logs, _time);
        log.Configure(true, 7);
        log.Append(EntryAt(31));
        var old = Path.Combine(Logs, "gapmouse-2026-10-24.log");
        File.WriteAllText(old, "x");

        log.Append(EntryAt(31));
        Assert.True(File.Exists(old));

        _time.Advance(TimeSpan.FromDays(1));
        log.Append(new LogEntry(new DateTimeOffset(2026, 11, 1, 1, 0, 0, TimeSpan.Zero), LogKind.Stop, "y"));

        Assert.False(File.Exists(old));
    }

    [Fact]
    public void Append_DirectoryNotWritable_DoesNotThrow()
    {
        var blocker = Path.Combine(_dir.Path, "blocker");
        File.WriteAllText(blocker, "x");
        var log = new FileActivityLog(Path.Combine(blocker, "logs"), _time);

        var ex = Record.Exception(() => log.Append(EntryAt(31)));

        Assert.Null(ex);
    }
}
