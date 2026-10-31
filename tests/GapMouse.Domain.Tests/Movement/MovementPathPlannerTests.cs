using GapMouse.Domain.Geometry;
using GapMouse.Domain.Movement;
using GapMouse.Testing.Randomness;
using Shouldly;

namespace GapMouse.Domain.Tests.Movement;

public class MovementPathPlannerTests
{
    private static readonly ScreenBounds Primary = new(0, 0, 1920, 1080);

    private static int[] Script(int[] head, int jitter, int settle) =>
        [.. head, .. Enumerable.Repeat(jitter, 21), settle];

    [Fact]
    public void Plan_StepCountIsStepsPlusOne_AndEndsAtDestination()
    {
        // dx=+100, dy=-50, ctrl offsets 0/0, steps=20, 21 jitters of 0, settle 300
        var values = Script([100, -50, 0, 0, 20], jitter: 0, settle: 300);
        var path = MovementPathPlanner.Plan(new PixelPoint(500, 500), Primary, 120, new QueueRandom(values));
        path.Steps.Count.ShouldBe(21);
        path.Steps[0].Position.ShouldBe(new PixelPoint(500, 500));
        path.Steps[^1].Position.ShouldBe(new PixelPoint(600, 450));
        path.SettleDelay.ShouldBe(TimeSpan.FromMilliseconds(300));
    }

    [Fact]
    public void Plan_DelayFollowsLegacyFormula()
    {
        var values = Script([0, 0, 0, 0, 20], jitter: 4, settle: 100);
        var path = MovementPathPlanner.Plan(new PixelPoint(500, 500), Primary, 120, new QueueRandom(values));
        path.Steps[0].DelayAfter.ShouldBe(TimeSpan.FromMilliseconds(12)); // 8 + 4 - sin(0)*6
        path.Steps[10].DelayAfter.ShouldBe(TimeSpan.FromMilliseconds(6)); // t=0.5 -> 8 + 4 - 6
    }

    [Fact]
    public void Plan_RequestsLegacyRanges()
    {
        // MinRandom: dx=dy=-120, ctrl=-60, steps=20, jitter=0, settle=100
        var minPath = MovementPathPlanner.Plan(new PixelPoint(500, 500), Primary, 120, new MinRandom());
        minPath.Steps.Count.ShouldBe(21);
        minPath.Steps[^1].Position.ShouldBe(new PixelPoint(380, 380));
        minPath.SettleDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
        var maxPath = MovementPathPlanner.Plan(new PixelPoint(500, 500), Primary, 120, new MaxRandom());
        maxPath.Steps.Count.ShouldBe(41);
        maxPath.SettleDelay.ShouldBe(TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void Plan_NearEdgeOfNegativeMonitor_EveryPointStaysInside()
    {
        var left = new ScreenBounds(-1920, -200, 0, 880);
        var path = MovementPathPlanner.Plan(new PixelPoint(-1915, -195), left, 120, new MinRandom());
        path.Steps.ShouldAllBe(s => s.Position.X >= -1920 && s.Position.X <= -1 && s.Position.Y >= -200 && s.Position.Y <= 879);
        path.Steps[^1].Position.ShouldBe(new PixelPoint(-1920, -200));
    }

    [Fact]
    public void Plan_ControlPointOutsideMonitor_CurvePointsAreClamped()
    {
        // origin at the right edge, dx=0, ctrl offset +60 pushes the curve past Right
        var values = Script([0, 0, 60, 0, 20], jitter: 0, settle: 100);
        var path = MovementPathPlanner.Plan(new PixelPoint(1919, 500), Primary, 120, new QueueRandom(values));
        path.Steps.ShouldAllBe(s => s.Position.X <= 1919);
    }
}
