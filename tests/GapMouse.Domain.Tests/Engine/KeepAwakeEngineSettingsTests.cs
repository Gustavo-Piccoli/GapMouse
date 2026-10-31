using GapMouse.Domain.Engine;
using GapMouse.Domain.Geometry;
using GapMouse.Domain.Settings;
using GapMouse.Testing.Randomness;
using Shouldly;
using static GapMouse.Domain.Tests.Engine.EngineTestContext;

namespace GapMouse.Domain.Tests.Engine;

public class KeepAwakeEngineSettingsTests
{
    private static EngineSettings With(int idle = 120, int amplitude = 120, bool display = true) =>
        EngineSettings.Create(idle, 1, 10, 25, amplitude, display).Value;

    /// <summary>Advances in 10 ms ticks (from tick 4) until the path is exhausted.</summary>
    private static (List<PixelPoint> Moves, TimeSpan SettleAt, EngineStep Settle) FinishPath(
        KeepAwakeEngine engine,
        List<PixelPoint> moves)
    {
        for (var tick = 4; tick <= 1000; tick++)
        {
            var at = 120.S() + TimeSpan.FromMilliseconds(tick * 10);
            var step = engine.Advance(Input(at, idle: at));
            if (step.MoveCursorTo is not { } point)
            {
                return (moves, at, step);
            }

            moves.Add(point);
        }

        throw new InvalidOperationException("The path never finished.");
    }

    private static (KeepAwakeEngine Engine, List<PixelPoint> Moves) MovingWithFourMovesDone()
    {
        var engine = new KeepAwakeEngine(With(), new MinRandom(), T0);
        List<PixelPoint> moves = [engine.Advance(Input(at: 120.S(), idle: 120.S())).MoveCursorTo!.Value];
        for (var tick = 1; tick <= 3; tick++)
        {
            var at = 120.S() + TimeSpan.FromMilliseconds(tick * 10);
            moves.Add(engine.Advance(Input(at, idle: at)).MoveCursorTo!.Value);
        }

        return (engine, moves);
    }

    [Fact]
    public void ApplySettings_DuringPath_RemainingStepsUnchanged()
    {
        var (unchanged, unchangedMoves) = MovingWithFourMovesDone();
        var (changed, changedMoves) = MovingWithFourMovesDone();
        changed.ApplySettings(With(amplitude: 500), T0 + 120.04.S());

        var a = FinishPath(unchanged, unchangedMoves);
        var b = FinishPath(changed, changedMoves);

        b.Moves.ShouldBe(a.Moves);
        b.SettleAt.ShouldBe(a.SettleAt);
        b.Settle.ShouldBe(a.Settle);

        var nextAt = a.SettleAt + a.Settle.WaitBeforeNext + 10.S();
        var nextUnchanged = NextPathMoves(unchanged, nextAt);
        var nextChanged = NextPathMoves(changed, nextAt);
        nextUnchanged.Count.ShouldBe(6);
        nextChanged.ShouldNotBe(nextUnchanged);
    }

    private static List<PixelPoint> NextPathMoves(KeepAwakeEngine engine, TimeSpan startAt)
    {
        List<PixelPoint> moves = [];
        for (var tick = 0; tick < 6; tick++)
        {
            var at = startAt + TimeSpan.FromMilliseconds(tick * 10);
            moves.Add(engine.Advance(Input(at, idle: at)).MoveCursorTo!.Value);
        }

        return moves;
    }

    [Fact]
    public void ApplySettings_NewThreshold_UsedOnNextWaitingCheck()
    {
        var e = NewEngine();
        e.ApplySettings(With(idle: 300), T0 + 1.S());

        e.Advance(Input(at: 200.S(), idle: 200.S())).MoveCursorTo.ShouldBeNull();
        e.State.ShouldBe(EngineState.Waiting);
        e.GetSnapshot().IdleThreshold.ShouldBe(300.S());

        e.Advance(Input(at: 300.S(), idle: 300.S())).MoveCursorTo.ShouldNotBeNull();
        e.State.ShouldBe(EngineState.Moving);
    }

    [Fact]
    public void ApplySettings_DisplayOff_PowerChangesOnNextAdvance()
    {
        var e = NewEngine();
        e.Advance(Input(at: 1.S(), idle: 1.S())).Power.ShouldBe(PowerRequest.KeepSystemAndDisplayAwake);

        e.ApplySettings(With(display: false), T0 + 1.5.S());

        e.Advance(Input(at: 2.S(), idle: 2.S())).Power.ShouldBe(PowerRequest.KeepSystemAwake);
    }

    [Fact]
    public void ApplySettings_QueuesSettingsChangedEvent()
    {
        var e = NewEngine();
        e.DrainEvents();
        var settings = With(idle: 60);

        e.ApplySettings(settings, T0 + 1.S());

        e.Settings.ShouldBe(settings);
        e.DrainEvents().ShouldBe([new SettingsChanged(T0 + 1.S(), settings)]);
    }

    [Fact]
    public void Stop_IsIdempotentAndReleasesPower()
    {
        var e = StartedMoving();

        e.Stop(T0 + 121.S());
        e.Stop(T0 + 122.S());
        var step = e.Advance(Input(at: 123.S(), idle: 500.S()));

        e.DrainEvents().OfType<EngineStopped>().Single().At.ShouldBe(T0 + 121.S());
        step.ShouldBe(new EngineStep(null, PowerRequest.None, 1.S()));
        e.IsStopped.ShouldBeTrue();
    }
}
