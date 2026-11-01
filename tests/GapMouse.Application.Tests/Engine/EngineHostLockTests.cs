using GapMouse.Application.Engine;
using GapMouse.Application.Logging;
using GapMouse.Application.Tests.Fakes;
using GapMouse.Domain.Engine;
using GapMouse.Domain.Settings;
using GapMouse.Testing.Randomness;
using Microsoft.Extensions.Time.Testing;

namespace GapMouse.Application.Tests.Engine;

public sealed class EngineHostLockTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly FakeIdle _idle = new();
    private readonly FakeCursor _cursor = new();
    private readonly FakeMonitors _monitors = new();
    private readonly RecordingPower _power = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero));
    private readonly List<LogEntry> _entries = [];
    private EngineHost? _host;

    public void Dispose() => _host?.Dispose();

    [Fact]
    public void SetSessionLocked_StopsCursorAndMonitorReads()
    {
        _idle.Idle = TimeSpan.FromHours(1);
        var host = Create(EngineSettings.Default);
        host.Start();
        SpinWait.SpinUntil(() => _cursor.SetCount > 0, Timeout).ShouldBeTrue();

        host.SetSessionLocked(true);

        SpinWait.SpinUntil(() => host.Snapshot.IsSessionLocked && _power.Last == PowerRequest.None, Timeout).ShouldBeTrue();
        var gets = _cursor.GetCount;
        var bounds = _monitors.GetCount;
        var sets = _cursor.SetCount;
        Thread.Sleep(300);
        _cursor.GetCount.ShouldBe(gets);
        _monitors.GetCount.ShouldBe(bounds);
        _cursor.SetCount.ShouldBe(sets);
        _power.Last.ShouldBe(PowerRequest.None);
    }

    [Fact]
    public void LockedWithKeepAwake_AppliesSystemOnly()
    {
        var host = Create(EngineSettings.Create(120, 1, 10, 25, 120, keepDisplayOn: true, keepAwakeWhileLocked: true).Value);
        host.Start();
        SpinWait.SpinUntil(() => _power.Last == PowerRequest.KeepSystemAndDisplayAwake, Timeout).ShouldBeTrue();

        host.SetSessionLocked(true);

        SpinWait.SpinUntil(() => _power.Last == PowerRequest.KeepSystemAwake, Timeout).ShouldBeTrue();
    }

    [Fact]
    public void LockedBeforeStart_FirstIterationReadsNoCursor()
    {
        var host = Create(EngineSettings.Default);
        host.SetSessionLocked(true);

        host.Start();

        SpinWait.SpinUntil(() => host.Snapshot.IsSessionLocked && _power.Last == PowerRequest.None, Timeout).ShouldBeTrue();
        Thread.Sleep(200);
        _cursor.GetCount.ShouldBe(0);
        _monitors.GetCount.ShouldBe(0);
        _cursor.SetCount.ShouldBe(0);
    }

    [Fact]
    public void Unlock_ResumesReadsAndMovement()
    {
        _idle.Idle = TimeSpan.FromHours(1);
        var host = Create(EngineSettings.Default);
        host.SetSessionLocked(true);
        host.Start();
        SpinWait.SpinUntil(() => host.Snapshot.IsSessionLocked, Timeout).ShouldBeTrue();

        host.SetSessionLocked(false);

        SpinWait.SpinUntil(() => _cursor.GetCount > 0 && !host.Snapshot.IsSessionLocked, Timeout).ShouldBeTrue();
        // The idle count restarts at unlock: a high system idle alone must not move the cursor yet.
        _cursor.SetCount.ShouldBe(0);

        _time.Advance(EngineSettings.Default.IdleThreshold + TimeSpan.FromSeconds(1));

        SpinWait.SpinUntil(() => _cursor.SetCount > 0, Timeout).ShouldBeTrue();
    }

    [Fact]
    public void PortRecovery_IsWrittenToActivityLog()
    {
        _cursor.ThrowOnNextGet();
        var host = Create(EngineSettings.Default);

        host.Start();

        SpinWait.SpinUntil(() => Entries().Count >= 2, Timeout).ShouldBeTrue();
        var entries = Entries();
        entries[0].Kind.ShouldBe(LogKind.Warn);
        entries[0].Message.ShouldBe("Port Cursor failed: Acesso negado.");
        entries[1].Kind.ShouldBe(LogKind.Warn);
        entries[1].Message.ShouldBe("Port Cursor recovered");
    }

    private EngineHost Create(EngineSettings settings)
    {
        var logger = new ActivityLogger<EngineHost>(new ListLog(_entries), _time);
        _host = new EngineHost(settings, _idle, _cursor, _monitors, _power, new MinRandom(), _time, logger);
        return _host;
    }

    private List<LogEntry> Entries()
    {
        lock (_entries)
        {
            return [.. _entries];
        }
    }

    private sealed class ListLog(List<LogEntry> entries) : IActivityLog
    {
        public void Append(LogEntry entry)
        {
            lock (entries)
            {
                entries.Add(entry);
            }
        }
    }
}
