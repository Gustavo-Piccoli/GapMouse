using GapMouse.Domain.Movement;
using GapMouse.Domain.Pausing;
using GapMouse.Domain.Randomness;
using GapMouse.Domain.Settings;

namespace GapMouse.Domain.Engine;

/// <summary>Pure, synchronous keep-awake state machine. Not thread-safe: the host serializes all calls.</summary>
public sealed class KeepAwakeEngine
{
    private static readonly TimeSpan UserReturnTolerance = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan InterMoveReturnIdle = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxStatsGap = TimeSpan.FromSeconds(60);

    private readonly IRandomSource _random;
    private readonly List<EngineEvent> _events = [];
    private readonly DateTimeOffset _startedAt;

    private MovementPhase _phase;
    private MovementPath? _path;
    private int _nextStepIndex;
    private DateTimeOffset _movementStartedAt;
    private DateTimeOffset _waitEndsAt;

    private DateTimeOffset? _pauseEndsAt;
    private DateTimeOffset? _lastPausedAt;
    private DateTimeOffset? _lastResumedAt;

    private DateTimeOffset? _lastAdvanceAt;
    private bool _lastAdvanceWasMoving;
    private TimeSpan _lastIdle;
    private DateTimeOffset? _lastMovingStartedAt;
    private DateTimeOffset? _lastUserReturnedAt;
    private DailyStats _today;

    public KeepAwakeEngine(EngineSettings settings, IRandomSource random, DateTimeOffset startedAt, DailyStats? restoredStats = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(random);
        Settings = settings;
        _random = random;
        _startedAt = startedAt;
        _today = restoredStats ?? DailyStats.Empty(DateOnly.FromDateTime(startedAt.Date));
        State = EngineState.Waiting;
        _events.Add(new EngineStarted(startedAt));
        _events.Add(new WaitingStarted(startedAt, settings.IdleThreshold));
    }

    private enum MovementPhase
    {
        ExecutingPath,
        InterMoveWait,
    }

    public EngineState State { get; private set; }

    public EngineSettings Settings { get; private set; }

    public bool IsStopped { get; private set; }

    public bool IsSessionLocked { get; private set; }

    private PowerRequest Power =>
        Settings.KeepDisplayOn ? PowerRequest.KeepSystemAndDisplayAwake : PowerRequest.KeepSystemAwake;

    public EngineStep Advance(EngineInput input)
    {
        if (IsStopped)
        {
            return new EngineStep(null, PowerRequest.None, Settings.CheckInterval);
        }

        if (IsSessionLocked)
        {
            return AdvanceLocked(input.Now);
        }

        var now = input.Now;
        RollDayIfNeeded(now);

        if (_lastAdvanceWasMoving && State == EngineState.Moving && _lastAdvanceAt is { } last)
        {
            var delta = now - last;
            if (delta > TimeSpan.Zero)
            {
                _today = _today with { MovingTime = _today.MovingTime + (delta > MaxStatsGap ? MaxStatsGap : delta) };
            }
        }

        _lastIdle = input.Idle;
        var step = State switch
        {
            EngineState.Paused => AdvancePaused(input),
            EngineState.Moving => AdvanceMoving(input),
            _ => AdvanceWaiting(input),
        };
        _lastAdvanceAt = now;
        _lastAdvanceWasMoving = State == EngineState.Moving;
        return step;
    }

    /// <summary>Advances while the session is locked: never moves the cursor and needs only the clock.</summary>
    public EngineStep AdvanceLocked(DateTimeOffset now)
    {
        if (IsStopped)
        {
            return new EngineStep(null, PowerRequest.None, Settings.CheckInterval);
        }

        RollDayIfNeeded(now);
        _lastAdvanceAt = now;
        _lastAdvanceWasMoving = false;

        if (State == EngineState.Paused && _pauseEndsAt is { } endsAt && endsAt <= now)
        {
            EnterWaitingFromPause(now, ResumeReason.PauseExpired);
        }

        if (State == EngineState.Paused)
        {
            var wait = _pauseEndsAt is { } ends ? Min(Settings.CheckInterval, ends - now) : Settings.CheckInterval;
            return new EngineStep(null, PowerRequest.None, wait);
        }

        return new EngineStep(
            null,
            Settings.KeepAwakeWhileLocked ? PowerRequest.KeepSystemAwake : PowerRequest.None,
            Settings.CheckInterval);
    }

    public void SetSessionLocked(bool locked, DateTimeOffset now)
    {
        if (locked == IsSessionLocked)
        {
            return;
        }

        IsSessionLocked = locked;
        _lastAdvanceWasMoving = false;
        if (locked)
        {
            if (State == EngineState.Moving)
            {
                State = EngineState.Waiting;
                _path = null;
            }

            _events.Add(new SessionLocked(now, Settings.KeepAwakeWhileLocked));
            return;
        }

        _events.Add(new SessionUnlocked(now));
        if (State == EngineState.Waiting)
        {
            _events.Add(new WaitingStarted(now, Settings.IdleThreshold));
        }
    }

    public IReadOnlyList<EngineEvent> DrainEvents()
    {
        var drained = _events.ToArray();
        _events.Clear();
        return drained;
    }

    public EngineSnapshot GetSnapshot() => new(
        State,
        _startedAt,
        _lastIdle,
        Settings.IdleThreshold,
        _lastMovingStartedAt,
        _lastUserReturnedAt,
        _lastPausedAt,
        _lastResumedAt,
        _pauseEndsAt,
        _today,
        IsSessionLocked);

    public void Pause(PauseDuration duration, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(duration);
        State = EngineState.Paused;
        _path = null;
        _pauseEndsAt = duration.Length is { } length ? now + length : null;
        _lastPausedAt = now;
        _events.Add(new EnginePaused(now, duration, _pauseEndsAt));
    }

    public bool Resume(DateTimeOffset now)
    {
        if (State != EngineState.Paused)
        {
            return false;
        }

        EnterWaitingFromPause(now, ResumeReason.Manual);
        return true;
    }

    public void ApplySettings(EngineSettings settings, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Settings = settings;
        _events.Add(new SettingsChanged(now, settings));
    }

    public void Stop(DateTimeOffset now)
    {
        if (IsStopped)
        {
            return;
        }

        IsStopped = true;
        _events.Add(new EngineStopped(now));
    }

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

    private void RollDayIfNeeded(DateTimeOffset now)
    {
        var day = DateOnly.FromDateTime(now.Date);
        if (day != _today.Day)
        {
            _today = DailyStats.Empty(day);
        }
    }

    private void EnterWaitingFromPause(DateTimeOffset now, ResumeReason reason)
    {
        State = EngineState.Waiting;
        _pauseEndsAt = null;
        _lastResumedAt = now;
        _events.Add(new EngineResumed(now, reason));
        _events.Add(new WaitingStarted(now, Settings.IdleThreshold));
    }

    private EngineStep AdvancePaused(EngineInput input)
    {
        if (_pauseEndsAt is not { } endsAt)
        {
            return new EngineStep(null, PowerRequest.None, Settings.CheckInterval);
        }

        if (endsAt <= input.Now)
        {
            EnterWaitingFromPause(input.Now, ResumeReason.PauseExpired);
            return AdvanceWaiting(input);
        }

        return new EngineStep(null, PowerRequest.None, Min(Settings.CheckInterval, endsAt - input.Now));
    }

    private EngineStep AdvanceWaiting(EngineInput input)
    {
        if (input.Idle < Settings.IdleThreshold)
        {
            return new EngineStep(null, Power, Settings.CheckInterval);
        }

        State = EngineState.Moving;
        _today = _today with { MovingActivations = _today.MovingActivations + 1 };
        _lastMovingStartedAt = input.Now;
        _events.Add(new MovingStarted(input.Now, Settings.MovementIntervalMin, Settings.MovementIntervalMax));
        return BeginPath(input);
    }

    private EngineStep AdvanceMoving(EngineInput input)
    {
        if (_phase == MovementPhase.ExecutingPath)
        {
            return AdvancePath(input);
        }

        if (input.Idle < InterMoveReturnIdle)
        {
            return ReturnToWaiting(input);
        }

        if (input.Now >= _waitEndsAt)
        {
            return BeginPath(input);
        }

        return new EngineStep(null, Power, Min(Settings.CheckInterval, _waitEndsAt - input.Now));
    }

    private EngineStep AdvancePath(EngineInput input)
    {
        var path = _path!;
        if (_nextStepIndex >= path.Steps.Count)
        {
            var interval = TimeSpan.FromSeconds(_random.NextInclusive(
                (int)Settings.MovementIntervalMin.TotalSeconds,
                (int)Settings.MovementIntervalMax.TotalSeconds));
            _waitEndsAt = input.Now + path.SettleDelay + interval;
            _phase = MovementPhase.InterMoveWait;
            _path = null;
            return new EngineStep(null, Power, path.SettleDelay);
        }

        if (input.Idle < (input.Now - _movementStartedAt) - UserReturnTolerance)
        {
            return ReturnToWaiting(input);
        }

        var step = path.Steps[_nextStepIndex++];
        return new EngineStep(step.Position, Power, step.DelayAfter);
    }

    private EngineStep BeginPath(EngineInput input)
    {
        _path = MovementPathPlanner.Plan(input.Cursor, input.Monitor, Settings.AmplitudePixels, _random);
        _movementStartedAt = input.Now;
        _phase = MovementPhase.ExecutingPath;
        var first = _path.Steps[0];
        _nextStepIndex = 1;
        return new EngineStep(first.Position, Power, first.DelayAfter);
    }

    private EngineStep ReturnToWaiting(EngineInput input)
    {
        State = EngineState.Waiting;
        _path = null;
        _lastUserReturnedAt = input.Now;
        _events.Add(new UserReturned(input.Now));
        _events.Add(new WaitingStarted(input.Now, Settings.IdleThreshold));
        return new EngineStep(null, Power, Settings.CheckInterval);
    }
}
