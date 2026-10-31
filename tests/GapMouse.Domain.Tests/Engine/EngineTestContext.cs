using GapMouse.Domain.Engine;
using GapMouse.Domain.Geometry;
using GapMouse.Domain.Settings;
using GapMouse.Testing.Randomness;

namespace GapMouse.Domain.Tests.Engine;

internal static class EngineTestContext
{
    public static readonly DateTimeOffset T0 = new(2026, 11, 2, 9, 0, 0, TimeSpan.FromHours(-3));

    public static readonly ScreenBounds Monitor = new(0, 0, 1920, 1080);

    public static readonly PixelPoint Cursor = new(960, 540);

    public static TimeSpan S(this int seconds) => TimeSpan.FromSeconds(seconds);

    public static TimeSpan S(this double seconds) => TimeSpan.FromMilliseconds(Math.Round(seconds * 1000));

    public static TimeSpan Ms(this int milliseconds) => TimeSpan.FromMilliseconds(milliseconds);

    public static KeepAwakeEngine NewEngine(EngineSettings? settings = null, DateTimeOffset? startedAt = null) =>
        new(settings ?? EngineSettings.Default, new MinRandom(), startedAt ?? T0);

    public static EngineInput Input(TimeSpan at, TimeSpan idle) => InputAt(T0 + at, idle);

    public static EngineInput InputAt(DateTimeOffset now, TimeSpan idle) => new(now, idle, Cursor, Monitor);

    /// <summary>An engine that entered Moving at 120 s with idle 120 s, events drained.</summary>
    public static KeepAwakeEngine StartedMoving(EngineSettings? settings = null)
    {
        var engine = NewEngine(settings);
        engine.Advance(Input(at: 120.S(), idle: 120.S()));
        engine.DrainEvents();
        return engine;
    }

    /// <summary>
    /// Feeds growing-idle inputs 10 ms apart, starting right after the Moving start at 120 s,
    /// until the path is exhausted; returns the settle step and the time it was produced.
    /// </summary>
    public static (EngineStep Settle, TimeSpan At) RunPath(KeepAwakeEngine engine)
    {
        for (var tick = 1; tick <= 1000; tick++)
        {
            var at = 120.S() + TimeSpan.FromMilliseconds(tick * 10);
            var step = engine.Advance(Input(at, idle: at));
            if (step.MoveCursorTo is null)
            {
                return (step, at);
            }
        }

        throw new InvalidOperationException("The path never finished.");
    }

    public static EngineStep RunUntilPathFinishes(KeepAwakeEngine engine) => RunPath(engine).Settle;
}
