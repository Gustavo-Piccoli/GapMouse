using GapMouse.Domain.Engine;
using GapMouse.Domain.Settings;
using Shouldly;
using static GapMouse.Domain.Tests.Engine.EngineTestContext;

namespace GapMouse.Domain.Tests.Engine;

public class KeepAwakeEngineMovementTests
{
    [Fact]
    public void Ctor_QueuesStartedThenWaiting() =>
        NewEngine().DrainEvents().Select(e => e.GetType()).ShouldBe([typeof(EngineStarted), typeof(WaitingStarted)]);

    [Fact]
    public void Advance_IdleBelowThreshold_StaysWaitingAndHoldsDisplayAwake()
    {
        var step = NewEngine().Advance(Input(at: 1.S(), idle: 119.S()));
        step.ShouldBe(new EngineStep(null, PowerRequest.KeepSystemAndDisplayAwake, 1.S()));
    }

    [Fact]
    public void Advance_IdleReachesThreshold_StartsMovingAndMovesCursor()
    {
        var e = NewEngine();
        var step = e.Advance(Input(at: 120.S(), idle: 120.S()));
        e.State.ShouldBe(EngineState.Moving);
        step.MoveCursorTo.ShouldNotBeNull();
        e.DrainEvents().OfType<MovingStarted>().Single().IntervalMin.ShouldBe(10.S());
        e.GetSnapshot().Today.MovingActivations.ShouldBe(1);
    }

    [Fact]
    public void Advance_IdleDropsDuringPath_ReturnsToWaitingWithoutMoving()
    {
        var e = StartedMoving();

        // 1 s into the movement the idle clock should read at least 0.5 s; 0.1 s means real input.
        var step = e.Advance(Input(at: 121.S(), idle: 0.1.S()));

        step.MoveCursorTo.ShouldBeNull();
        e.State.ShouldBe(EngineState.Waiting);
        e.DrainEvents().ShouldContain(ev => ev is UserReturned);
    }

    [Fact]
    public void Advance_IdleKeepsGrowingDuringPath_DoesNotAbort()
    {
        var e = StartedMoving();
        e.Advance(Input(at: 120.3.S(), idle: 120.3.S())).MoveCursorTo.ShouldNotBeNull();
    }

    [Fact]
    public void Advance_AfterPath_SettlesThenWaitsMinInterval()
    {
        var e = StartedMoving();
        var (settle, settledAt) = RunPath(e);

        settle.ShouldBe(new EngineStep(null, PowerRequest.KeepSystemAndDisplayAwake, 100.Ms()));
        e.State.ShouldBe(EngineState.Moving);

        // MinRandom gives a 10 s interval after the 100 ms settle: still waiting 5 s in.
        var waiting = e.Advance(Input(settledAt + 5.S(), idle: settledAt + 5.S()));
        waiting.MoveCursorTo.ShouldBeNull();
        waiting.WaitBeforeNext.ShouldBe(1.S());

        // Close to the end of the wait the delay is shortened to what remains (0.1 s + 10 s - 9.5 s).
        var nearEnd = e.Advance(Input(settledAt + 9.5.S(), idle: settledAt + 9.5.S()));
        nearEnd.MoveCursorTo.ShouldBeNull();
        nearEnd.WaitBeforeNext.ShouldBe(600.Ms());

        // Past the end of the wait a new path starts.
        e.Advance(Input(settledAt + 10.2.S(), idle: settledAt + 10.2.S())).MoveCursorTo.ShouldNotBeNull();
    }

    [Fact]
    public void Advance_InputDuringInterMoveWait_ReturnsToWaiting()
    {
        var e = StartedMoving();
        var (_, settledAt) = RunPath(e);
        e.DrainEvents();

        var step = e.Advance(Input(settledAt + 3.S(), idle: 0.5.S()));

        step.ShouldBe(new EngineStep(null, PowerRequest.KeepSystemAndDisplayAwake, 1.S()));
        e.State.ShouldBe(EngineState.Waiting);
        e.DrainEvents().Select(ev => ev.GetType()).ShouldBe([typeof(UserReturned), typeof(WaitingStarted)]);
    }

    [Fact]
    public void Advance_NewMovementAfterWait_IsNotANewActivation()
    {
        var e = StartedMoving();
        var (_, settledAt) = RunPath(e);

        var step = e.Advance(Input(settledAt + 10.2.S(), idle: settledAt + 10.2.S()));

        step.MoveCursorTo.ShouldNotBeNull();
        e.State.ShouldBe(EngineState.Moving);
        e.GetSnapshot().Today.MovingActivations.ShouldBe(1);
        e.DrainEvents().OfType<MovingStarted>().ShouldBeEmpty();
    }

    [Fact]
    public void Advance_KeepDisplayOff_HoldsSystemOnly()
    {
        var settings = EngineSettings.Create(120, 1, 10, 25, 120, keepDisplayOn: false).Value;
        var e = NewEngine(settings);

        e.Advance(Input(at: 1.S(), idle: 1.S())).Power.ShouldBe(PowerRequest.KeepSystemAwake);
        e.Advance(Input(at: 120.S(), idle: 120.S())).Power.ShouldBe(PowerRequest.KeepSystemAwake);
    }

    [Fact]
    public void Advance_CrossingMidnight_ResetsDailyStats()
    {
        var start = new DateTimeOffset(2026, 11, 2, 23, 58, 0, TimeSpan.FromHours(-3));
        var e = NewEngine(startedAt: start);
        e.Advance(InputAt(new DateTimeOffset(2026, 11, 2, 23, 59, 30, TimeSpan.FromHours(-3)), 130.S()));
        e.GetSnapshot().Today.MovingActivations.ShouldBe(1);

        e.Advance(InputAt(new DateTimeOffset(2026, 11, 3, 0, 0, 10, TimeSpan.FromHours(-3)), 170.S()));

        var today = e.GetSnapshot().Today;
        today.Day.ShouldBe(new DateOnly(2026, 11, 3));
        today.MovingActivations.ShouldBe(0);
        today.MovingTime.ShouldBe(40.S());
    }

    [Fact]
    public void Advance_ClockMovesBackwards_DoesNotAbortOrThrow()
    {
        var e = StartedMoving();

        var step = e.Advance(Input(at: 100.S(), idle: 500.S()));

        step.MoveCursorTo.ShouldNotBeNull();
        e.State.ShouldBe(EngineState.Moving);
        e.GetSnapshot().Today.MovingTime.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void Advance_ConsecutiveMovingCalls_AccumulateMovingTimeCappedAtSixtySeconds()
    {
        var e = StartedMoving();

        e.Advance(Input(at: 120.5.S(), idle: 120.5.S()));
        e.GetSnapshot().Today.MovingTime.ShouldBe(0.5.S());

        e.Advance(Input(at: 300.S(), idle: 300.S()));
        e.GetSnapshot().Today.MovingTime.ShouldBe(60.5.S());
    }
}
