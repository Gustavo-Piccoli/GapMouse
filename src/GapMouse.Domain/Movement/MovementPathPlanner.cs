using GapMouse.Domain.Geometry;
using GapMouse.Domain.Randomness;

namespace GapMouse.Domain.Movement;

/// <summary>Plans a natural quadratic Bézier cursor path; a port of the legacy PowerShell algorithm.</summary>
public static class MovementPathPlanner
{
    private const int MinSteps = 20;
    private const int MaxSteps = 40;
    private const int BaseDelayMs = 8;
    private const int MaxJitterMs = 4;
    private const double DelayVariationMs = 6;
    private const int MinSettleMs = 100;
    private const int MaxSettleMs = 500;

    /// <remarks>Random values are drawn in this order: dx, dy, control offset X, control offset Y, steps, one jitter per point, settle.</remarks>
    public static MovementPath Plan(PixelPoint origin, ScreenBounds bounds, int amplitudePixels, IRandomSource random)
    {
        var half = amplitudePixels / 2;
        var dx = random.NextInclusive(-amplitudePixels, amplitudePixels);
        var dy = random.NextInclusive(-amplitudePixels, amplitudePixels);
        var dest = bounds.Clamp(new PixelPoint(origin.X + dx, origin.Y + dy));

        var ctrlX = ((origin.X + dest.X) / 2.0) + random.NextInclusive(-half, half);
        var ctrlY = ((origin.Y + dest.Y) / 2.0) + random.NextInclusive(-half, half);

        var steps = random.NextInclusive(MinSteps, MaxSteps);
        var points = new MovementStep[steps + 1];
        for (var i = 0; i <= steps; i++)
        {
            var t = i / (double)steps;
            var u = 1 - t;
            var x = (u * u * origin.X) + (2 * u * t * ctrlX) + (t * t * dest.X);
            var y = (u * u * origin.Y) + (2 * u * t * ctrlY) + (t * t * dest.Y);
            var position = bounds.Clamp(new PixelPoint((int)Math.Round(x), (int)Math.Round(y)));
            var delayMs = Math.Round(BaseDelayMs + random.NextInclusive(0, MaxJitterMs) - (Math.Sin(t * Math.PI) * DelayVariationMs));
            points[i] = new MovementStep(position, TimeSpan.FromMilliseconds(delayMs));
        }

        var settle = TimeSpan.FromMilliseconds(random.NextInclusive(MinSettleMs, MaxSettleMs));
        return new MovementPath(points, settle);
    }
}
