using GapMouse.Domain.Engine;
using GapMouse.Domain.Pausing;
using GapMouse.Domain.Settings;
using Shouldly;
using static GapMouse.Domain.Tests.Engine.EngineTestContext;

namespace GapMouse.Domain.Tests.Engine;

public class KeepAwakeEngineLockTests
{
    private static EngineSettings LockedAwakeSettings() =>
        EngineSettings.Create(120, 1, 10, 25, 120, keepDisplayOn: true, keepAwakeWhileLocked: true).Value;

    [Fact]
    public void Lock_FromWaiting_EmitsSessionLocked_AndAdvanceLockedNeverMoves()
    {
        var e = NewEngine();
        e.DrainEvents();

        e.SetSessionLocked(true, T0 + 5.S());

        e.DrainEvents().ShouldBe([new SessionLocked(T0 + 5.S(), false)]);
        var step = e.AdvanceLocked(T0 + 600.S());
        step.MoveCursorTo.ShouldBeNull();
        step.Power.ShouldBe(PowerRequest.None);
        step.WaitBeforeNext.ShouldBe(e.Settings.CheckInterval);
    }

    [Fact]
    public void LockWhileMoving_ThenUnlock_RestartsWaiting()
    {
        var e = StartedMoving();

        e.SetSessionLocked(true, T0 + 121.S());

        e.State.ShouldBe(EngineState.Waiting);
        var lockEvents = e.DrainEvents();
        lockEvents.OfType<SessionLocked>().Count().ShouldBe(1);
        lockEvents.OfType<UserReturned>().ShouldBeEmpty();

        e.SetSessionLocked(false, T0 + 200.S());

        var unlockEvents = e.DrainEvents();
        unlockEvents.Count.ShouldBe(2);
        unlockEvents[0].ShouldBe(new SessionUnlocked(T0 + 200.S()));
        unlockEvents[1].ShouldBe(new WaitingStarted(T0 + 200.S(), e.Settings.IdleThreshold));
    }

    [Fact]
    public void LockWhilePaused_Indefinitely_UnlockStaysPaused()
    {
        var e = NewEngine();
        e.Pause(PauseDuration.Indefinite, T0);
        e.SetSessionLocked(true, T0 + 1.S());
        e.DrainEvents();

        e.SetSessionLocked(false, T0 + 2.S());

        e.State.ShouldBe(EngineState.Paused);
        e.DrainEvents().ShouldBe([new SessionUnlocked(T0 + 2.S())]);
    }

    [Fact]
    public void TimedPauseExpiresWhileLocked_ResumesButDoesNotMove()
    {
        var e = NewEngine();
        e.Pause(PauseDuration.TenMinutes, T0);
        e.SetSessionLocked(true, T0 + 1.S());
        e.DrainEvents();

        var step = e.AdvanceLocked(T0 + 600.S());

        step.MoveCursorTo.ShouldBeNull();
        e.State.ShouldBe(EngineState.Waiting);
        e.DrainEvents().OfType<EngineResumed>().Single().Reason.ShouldBe(ResumeReason.PauseExpired);
        e.AdvanceLocked(T0 + 700.S()).MoveCursorTo.ShouldBeNull();
        e.State.ShouldBe(EngineState.Waiting);
    }

    [Fact]
    public void Locked_KeepAwake_RequestsSystemOnly_EvenWithDisplayOn()
    {
        var e = NewEngine(LockedAwakeSettings());
        e.SetSessionLocked(true, T0 + 1.S());

        e.AdvanceLocked(T0 + 2.S()).Power.ShouldBe(PowerRequest.KeepSystemAwake);

        e.Pause(PauseDuration.Indefinite, T0 + 3.S());
        e.AdvanceLocked(T0 + 4.S()).Power.ShouldBe(PowerRequest.None);
    }

    [Fact]
    public void SetSessionLocked_IsIdempotent()
    {
        var e = NewEngine();
        e.SetSessionLocked(false, T0);
        e.DrainEvents().Count.ShouldBe(2);

        e.SetSessionLocked(true, T0 + 1.S());
        e.SetSessionLocked(true, T0 + 2.S());
        e.DrainEvents().Count.ShouldBe(1);

        e.SetSessionLocked(false, T0 + 3.S());
        e.SetSessionLocked(false, T0 + 4.S());
        e.DrainEvents().OfType<SessionUnlocked>().Count().ShouldBe(1);
    }

    [Fact]
    public void Advance_WhileLocked_BehavesLikeAdvanceLocked()
    {
        var e = NewEngine();
        e.SetSessionLocked(true, T0 + 1.S());

        var step = e.Advance(Input(at: 600.S(), idle: 600.S()));

        step.MoveCursorTo.ShouldBeNull();
        step.Power.ShouldBe(PowerRequest.None);
        e.State.ShouldBe(EngineState.Waiting);
    }

    [Fact]
    public void Locked_DoesNotAccumulateMovingTime()
    {
        var e = StartedMoving();
        e.Advance(Input(at: 121.S(), idle: 121.S()));
        e.SetSessionLocked(true, T0 + 122.S());
        var before = e.GetSnapshot().Today.MovingTime;

        e.AdvanceLocked(T0 + 130.S());
        e.AdvanceLocked(T0 + 500.S());
        e.GetSnapshot().Today.MovingTime.ShouldBe(before);

        e.SetSessionLocked(false, T0 + 600.S());
        e.Advance(Input(at: 1000.S(), idle: 5.S()));

        e.GetSnapshot().Today.MovingTime.ShouldBe(before);
    }

    [Fact]
    public void LockWhilePaused_WithKeepAwake_ReportsKeepAwakeFalse()
    {
        var e = NewEngine(LockedAwakeSettings());
        e.Pause(PauseDuration.Indefinite, T0);
        e.DrainEvents();

        e.SetSessionLocked(true, T0 + 1.S());

        e.DrainEvents().ShouldBe([new SessionLocked(T0 + 1.S(), false)]);
    }

    [Fact]
    public void Snapshot_ReportsIsSessionLocked()
    {
        var e = NewEngine();
        e.GetSnapshot().IsSessionLocked.ShouldBeFalse();

        e.SetSessionLocked(true, T0);
        e.IsSessionLocked.ShouldBeTrue();
        e.GetSnapshot().IsSessionLocked.ShouldBeTrue();

        e.SetSessionLocked(false, T0 + 1.S());
        e.GetSnapshot().IsSessionLocked.ShouldBeFalse();
    }
}
