using GapMouse.Domain.Randomness;

namespace GapMouse.Testing.Randomness;

/// <summary>Returns preset values in order and rejects out-of-range values, which exposes call-order mistakes.</summary>
public sealed class QueueRandom(params int[] values) : IRandomSource
{
    private readonly Queue<int> _values = new(values);

    public int NextInclusive(int minInclusive, int maxInclusive)
    {
        if (!_values.TryDequeue(out var value))
        {
            throw new InvalidOperationException("No more queued random values.");
        }

        if (value < minInclusive || value > maxInclusive)
        {
            throw new InvalidOperationException(
                $"Queued value {value} is outside the requested range [{minInclusive}, {maxInclusive}].");
        }

        return value;
    }
}
