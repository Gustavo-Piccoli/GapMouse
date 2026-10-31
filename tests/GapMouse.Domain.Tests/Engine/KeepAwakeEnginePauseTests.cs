using GapMouse.Domain.Engine;
using GapMouse.Domain.Pausing;
using Shouldly;
using static GapMouse.Domain.Tests.Engine.EngineTestContext;

namespace GapMouse.Domain.Tests.Engine;

public class KeepAwakeEnginePauseTests
{
    [Fact]
    public void Pause_DuringPath_NoFurtherCursorMovesAndPowerReleased()
    {
        var e = StartedMoving();
        e.Pause(PauseDuration.Indefinite, T0 + 120.1.S());

        var step = e.Advance(Input(at: 120.2.S(), idle: 500.S()));

        step.ShouldBe(new EngineStep(null, PowerRequest.None, 1.S()));
        e.State.ShouldBe(EngineState.Paused);
        e.GetSnapshot().PauseEndsAt.ShouldBeNull();
    }

    [Fact]
    public void Pause_Timed_ExpiresAndResumesWithReasonPauseExpired()
    {
        var e = NewEngine();
        e.Pause(PauseDuration.TenMinutes, T0);
        e.DrainEvents();

        e.Advance(Input(at: 599.S(), idle: 5.S())).Power.ShouldBe(PowerRequest.None);
        e.Advance(Input(at: 600.S(), idle: 5.S())).Power.ShouldBe(PowerRequest.KeepSystemAndDisplayAwake);

        e.DrainEvents().OfType<EngineResumed>().Single().Reason.ShouldBe(ResumeReason.PauseExpired);
    }

    [Fact]
    public void Pause_ExpiresWhileUserAway_MovesInSameAdvance()
    {
        var e = NewEngine();
        e.Pause(PauseDuration.TenMinutes, T0);

        e.Advance(Input(at: 600.S(), idle: 600.S())).MoveCursorTo.ShouldNotBeNull();

        e.State.ShouldBe(EngineState.Moving);
    }

    [Fact]
    public void Pause_TimedNearEnd_WaitIsShortenedToRemaining()
    {
        var e = NewEngine();
        e.Pause(PauseDuration.TenMinutes, T0);

        e.Advance(Input(at: 599.6.S(), idle: 5.S())).WaitBeforeNext.ShouldBe(400.Ms());
    }

    [Fact]
    public void Pause_WhilePaused_ReplacesDuration()
    {
        var e = NewEngine();
        e.Pause(PauseDuration.Indefinite, T0);
        e.Pause(PauseDuration.TenMinutes, T0 + 30.S());

        var snapshot = e.GetSnapshot();
        snapshot.PauseEndsAt.ShouldBe(T0 + 30.S() + TimeSpan.FromMinutes(10));
        snapshot.LastPausedAt.ShouldBe(T0 + 30.S());
        e.State.ShouldBe(EngineState.Paused);
    }

    [Fact]
    public void Pause_FromWaiting_QueuesEnginePausedWithResumeTime()
    {
        var e = NewEngine();
        e.DrainEvents();

        e.Pause(PauseDuration.ThirtyMinutes, T0 + 5.S());

        e.DrainEvents().ShouldBe([new EnginePaused(T0 + 5.S(), PauseDuration.ThirtyMinutes, T0 + 5.S() + TimeSpan.FromMinutes(30))]);
    }

    [Fact]
    public void Resume_WhenNotPaused_ReturnsFalseAndQueuesNothing()
    {
        var e = NewEngine();
        e.DrainEvents();

        e.Resume(T0 + 1.S()).ShouldBeFalse();

        e.DrainEvents().ShouldBeEmpty();
        e.State.ShouldBe(EngineState.Waiting);
        e.GetSnapshot().LastResumedAt.ShouldBeNull();
    }

    [Fact]
    public void Resume_Manual_GoesToWaitingWithReasonManual()
    {
        var e = NewEngine();
        e.Pause(PauseDuration.Indefinite, T0);
        e.DrainEvents();

        e.Resume(T0 + 20.S()).ShouldBeTrue();

        e.State.ShouldBe(EngineState.Waiting);
        e.DrainEvents().ShouldBe([new EngineResumed(T0 + 20.S(), ResumeReason.Manual), new WaitingStarted(T0 + 20.S(), 120.S())]);
        var snapshot = e.GetSnapshot();
        snapshot.LastResumedAt.ShouldBe(T0 + 20.S());
        snapshot.PauseEndsAt.ShouldBeNull();
    }

    [Fact]
    public void PausedTime_IsNotCountedInMovingTime()
    {
        var e = StartedMoving();
        e.Advance(Input(at: 130.S(), idle: 130.S()));
        e.Pause(PauseDuration.Indefinite, T0 + 131.S());

        e.Advance(Input(at: 200.S(), idle: 5.S()));
        e.Resume(T0 + 201.S());
        e.Advance(Input(at: 205.S(), idle: 5.S()));

        e.GetSnapshot().Today.MovingTime.ShouldBe(10.S());
    }
}
