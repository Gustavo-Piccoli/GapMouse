using GapMouse.Application.Engine;
using GapMouse.Application.Tests.Fakes;
using GapMouse.Domain.Engine;
using GapMouse.Domain.Pausing;
using GapMouse.Domain.Settings;
using GapMouse.Testing.Randomness;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace GapMouse.Application.Tests.Engine;

public sealed class EngineHostTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly FakeIdle _idle = new();
    private readonly FakeCursor _cursor = new();
    private readonly RecordingPower _power = new();
    private readonly ListLogger<EngineHost> _logger = new();
    private readonly List<EngineEvent> _events = [];
    private readonly object _eventsGate = new();
    private readonly EngineHost _host;

    public EngineHostTests()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero));
        _host = new EngineHost(
            EngineSettings.Default,
            _idle,
            _cursor,
            new FakeMonitors(),
            _power,
            new MinRandom(),
            time,
            _logger);
        _host.EventRaised += (_, e) =>
        {
            lock (_eventsGate)
            {
                _events.Add(e.Event);
            }
        };
    }

    public void Dispose() => _host.Dispose();

    [Fact]
    public void Start_AppliesDisplayAwakeAndRaisesStarted()
    {
        _host.Start();

        SpinWait.SpinUntil(() => _power.Calls.Count > 0, Timeout).ShouldBeTrue();
        SpinWait.SpinUntil(() => EventsSnapshot().OfType<WaitingStarted>().Any(), Timeout).ShouldBeTrue();
        _power.Calls[0].Request.ShouldBe(PowerRequest.KeepSystemAndDisplayAwake);
        var events = EventsSnapshot();
        events.OfType<EngineStarted>().Count().ShouldBe(1);
        events.OfType<WaitingStarted>().Count().ShouldBe(1);
    }

    [Fact]
    public void Pause_WhileMoving_StopsCursorAndReleasesPower()
    {
        _idle.Idle = TimeSpan.FromHours(1);
        _host.Start();
        SpinWait.SpinUntil(() => _cursor.SetCount > 0, Timeout).ShouldBeTrue();

        _host.Pause(PauseDuration.Indefinite);

        SpinWait.SpinUntil(() => _power.Last == PowerRequest.None, Timeout).ShouldBeTrue();
        var countAtPause = _cursor.SetCount;
        Thread.Sleep(300);
        _cursor.SetCount.ShouldBe(countAtPause);
        _host.Snapshot.State.ShouldBe(EngineState.Paused);
    }

    [Fact]
    public void Dispose_ReleasesPowerOnEngineThreadAndRaisesStopped()
    {
        _host.Start();
        SpinWait.SpinUntil(() => _power.Calls.Count > 0, Timeout).ShouldBeTrue();

        _host.Dispose();

        var calls = _power.Calls;
        calls.Select(c => c.ThreadId).Distinct().Count().ShouldBe(1);
        calls[0].ThreadId.ShouldNotBe(Environment.CurrentManagedThreadId);
        calls[^1].Request.ShouldBe(PowerRequest.None);
        EventsSnapshot().OfType<EngineStopped>().Count().ShouldBe(1);
    }

    [Fact]
    public void CursorThrows_LoopKeepsRunningAndLogsOncePerStreak()
    {
        _cursor.ThrowOnSet = true;
        _idle.Idle = TimeSpan.FromHours(1);
        _host.Start();
        SpinWait.SpinUntil(() => _cursor.SetCount >= 1, Timeout).ShouldBeTrue();

        Thread.Sleep(1500);

        _cursor.SetCount.ShouldBeGreaterThan(1);
        _host.Snapshot.State.ShouldBe(EngineState.Moving);
        _logger.Levels.Count(l => l == LogLevel.Warning).ShouldBe(1);
        _power.Last.ShouldBe(PowerRequest.KeepSystemAndDisplayAwake);
    }

    [Fact]
    public void ApplySettings_TakesEffectWithoutWaitingFullInterval()
    {
        _host.Start();
        SpinWait.SpinUntil(() => _power.Last == PowerRequest.KeepSystemAndDisplayAwake, Timeout).ShouldBeTrue();
        var settings = EngineSettings.Create(120, 60, 10, 25, 120, keepDisplayOn: false).Value;

        _host.ApplySettings(settings);

        SpinWait.SpinUntil(() => _power.Last == PowerRequest.KeepSystemAwake, TimeSpan.FromMilliseconds(500)).ShouldBeTrue();
    }

    [Fact]
    public void Start_Twice_Throws()
    {
        _host.Start();

        Should.Throw<InvalidOperationException>(() => _host.Start());
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(-50, 10)]
    [InlineData(10, 10)]
    [InlineData(1000, 1000)]
    public void ClampWait_OutOfRangeOrInRange_ReturnsValueWithinBounds(int inputMs, int expectedMs) =>
        EngineHost.ClampWait(TimeSpan.FromMilliseconds(inputMs)).ShouldBe(TimeSpan.FromMilliseconds(expectedMs));

    [Fact]
    public void ClampWait_InfiniteOrHuge_IsCappedAtMaxIntMilliseconds()
    {
        EngineHost.ClampWait(Timeout_Infinite).ShouldBe(TimeSpan.FromMilliseconds(10));
        EngineHost.ClampWait(TimeSpan.MaxValue).ShouldBe(TimeSpan.FromMilliseconds(int.MaxValue));
    }

    [Fact]
    public void QueuedCommandThrows_LoopKeepsRunningAndLogsError()
    {
        _host.Start();
        SpinWait.SpinUntil(() => _power.Last == PowerRequest.KeepSystemAndDisplayAwake, Timeout).ShouldBeTrue();

        _host.Enqueue((_, _) => throw new InvalidOperationException("command boom"));
        _host.ApplySettings(EngineSettings.Create(120, 60, 10, 25, 120, keepDisplayOn: false).Value);

        SpinWait.SpinUntil(() => _power.Last == PowerRequest.KeepSystemAwake, Timeout).ShouldBeTrue();
        _logger.Levels.Count(l => l == LogLevel.Error).ShouldBe(1);
    }

    [Fact]
    public void SubscriberThrows_LoopKeepsRunningAndSnapshotUpdates()
    {
        _host.EventRaised += (_, _) => throw new InvalidOperationException("subscriber boom");
        _host.Start();
        SpinWait.SpinUntil(() => _power.Last == PowerRequest.KeepSystemAndDisplayAwake, Timeout).ShouldBeTrue();

        _host.Pause(PauseDuration.Indefinite);

        SpinWait.SpinUntil(() => _host.Snapshot.State == EngineState.Paused, Timeout).ShouldBeTrue();
        SpinWait.SpinUntil(() => _power.Last == PowerRequest.None, Timeout).ShouldBeTrue();
    }

    [Fact]
    public void Commands_AfterDispose_DoNotThrow()
    {
        _host.Start();
        SpinWait.SpinUntil(() => _power.Calls.Count > 0, Timeout).ShouldBeTrue();
        _host.Dispose();

        Should.NotThrow(() =>
        {
            _host.Pause(PauseDuration.Indefinite);
            _host.Resume();
            _host.ApplySettings(EngineSettings.Default);
        });
    }

    [Fact]
    public void Dispose_CalledFromEngineThread_DoesNotDeadlockAndReleasesPower()
    {
        _host.EventRaised += (_, e) =>
        {
            if (e.Event is EngineStarted)
            {
                _host.Dispose();
            }
        };

        _host.Start();

        SpinWait.SpinUntil(() => _power.Last == PowerRequest.None, Timeout).ShouldBeTrue();
        _power.Calls.Select(c => c.ThreadId).Distinct().Count().ShouldBe(1);
    }

    [Fact]
    public void IdleReadThrows_SkipsAdvanceLogsOnceAndLogsRecovery()
    {
        _idle.ThrowOnRead = true;
        _host.Start();
        SpinWait.SpinUntil(() => _logger.Levels.Contains(LogLevel.Warning), Timeout).ShouldBeTrue();

        Thread.Sleep(300);

        _power.Calls.ShouldBeEmpty();
        _logger.Levels.Count(l => l == LogLevel.Warning).ShouldBe(1);

        _idle.ThrowOnRead = false;
        _host.ApplySettings(EngineSettings.Default);

        SpinWait.SpinUntil(() => _power.Last == PowerRequest.KeepSystemAndDisplayAwake, Timeout).ShouldBeTrue();
        _logger.Levels.Count(l => l == LogLevel.Information).ShouldBe(1);
        _logger.Levels.Count(l => l == LogLevel.Warning).ShouldBe(1);
    }

    private static readonly TimeSpan Timeout_Infinite = System.Threading.Timeout.InfiniteTimeSpan;

    private EngineEvent[] EventsSnapshot()
    {
        lock (_eventsGate)
        {
            return [.. _events];
        }
    }
}
