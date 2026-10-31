using GapMouse.Application.State;
using GapMouse.Application.Tests.Fakes;
using GapMouse.Domain.Engine;
using GapMouse.Domain.Pausing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace GapMouse.Application.Tests.State;

public sealed class AppStatePersisterTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void UserReturned_SavesCurrentStats()
    {
        var f = new Fixture();
        f.Persister.Start();

        f.Engine.Raise(new UserReturned(Now));

        Assert.Equal([f.Engine.Snapshot.Today], f.Store.Saved.Select(s => s.Today));
    }

    [Fact]
    public void Paused_SavesCurrentStats()
    {
        var f = new Fixture();
        f.Persister.Start();

        f.Engine.Raise(new EnginePaused(Now, PauseDuration.TenMinutes, null));

        Assert.Single(f.Store.Saved);
    }

    [Fact]
    public void WhileMoving_SavesEverySixtySeconds()
    {
        var f = new Fixture();
        f.Engine.Snapshot = f.Engine.Snapshot with { State = EngineState.Moving };
        f.Persister.Start();

        f.Time.Advance(TimeSpan.FromSeconds(59));
        Assert.Empty(f.Store.Saved);

        f.Time.Advance(TimeSpan.FromSeconds(1));
        Assert.Single(f.Store.Saved);

        f.Time.Advance(TimeSpan.FromSeconds(60));
        Assert.Equal(2, f.Store.Saved.Count);
    }

    [Fact]
    public void WhileWaiting_TimerDoesNotSave()
    {
        var f = new Fixture();
        f.Persister.Start();

        f.Time.Advance(TimeSpan.FromMinutes(5));

        Assert.Empty(f.Store.Saved);
    }

    [Fact]
    public void Dispose_SavesOnceAndStopsSaving()
    {
        var f = new Fixture();
        f.Engine.Snapshot = f.Engine.Snapshot with { State = EngineState.Moving };
        f.Persister.Start();

        f.Persister.Dispose();
        Assert.Single(f.Store.Saved);

        f.Engine.Raise(new UserReturned(Now));
        f.Engine.Raise(new EngineStopped(Now));
        f.Time.Advance(TimeSpan.FromMinutes(5));
        f.Persister.Dispose();

        Assert.Single(f.Store.Saved);
    }

    [Fact]
    public void StoreThrows_LogsOneWarningPerStreak()
    {
        var f = new Fixture();
        f.Persister.Start();
        f.Store.Failures.Enqueue(new IOException("disk"));
        f.Store.Failures.Enqueue(new UnauthorizedAccessException("denied"));
        f.Store.Failures.Enqueue(new IOException("disk"));

        f.Engine.Raise(new UserReturned(Now));
        f.Engine.Raise(new UserReturned(Now));
        f.Engine.Raise(new UserReturned(Now));
        Assert.Single(f.Logger.Levels, LogLevel.Warning);

        f.Engine.Raise(new UserReturned(Now));
        Assert.Single(f.Logger.Levels, LogLevel.Warning);

        f.Store.Failures.Enqueue(new IOException("disk"));
        f.Engine.Raise(new UserReturned(Now));

        Assert.Equal(2, f.Logger.Levels.Count(l => l == LogLevel.Warning));
    }

    [Fact]
    public void TimerSave_StoreThrowsNonIoException_DoesNotThrowAndLogsOneWarning()
    {
        var f = new Fixture();
        f.Engine.Snapshot = f.Engine.Snapshot with { State = EngineState.Moving };
        f.Persister.Start();
        f.Store.Failures.Enqueue(new ArgumentException("bad path"));
        f.Store.Failures.Enqueue(new NotSupportedException("nope"));

        var ex = Record.Exception(() => f.Time.Advance(TimeSpan.FromSeconds(120)));

        Assert.Null(ex);
        Assert.Single(f.Logger.Levels, LogLevel.Warning);
    }

    private sealed class RecordingStore : IAppStateStore
    {
        public List<AppState> Saved { get; } = [];

        public Queue<Exception> Failures { get; } = new();

        public AppState? Load() => null;

        public void Save(AppState state)
        {
            if (Failures.TryDequeue(out var failure))
            {
                throw failure;
            }

            Saved.Add(state);
        }
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Persister = new AppStatePersister(Engine, Store, Time, Logger);
        }

        public FakeEngineController Engine { get; } = new();

        public RecordingStore Store { get; } = new();

        public FakeTimeProvider Time { get; } = new(Now);

        public ListLogger<AppStatePersister> Logger { get; } = new();

        public AppStatePersister Persister { get; }
    }
}
