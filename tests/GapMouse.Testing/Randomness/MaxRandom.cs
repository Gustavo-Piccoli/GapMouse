using GapMouse.Domain.Randomness;

namespace GapMouse.Testing.Randomness;

/// <summary>Always returns the upper bound.</summary>
public sealed class MaxRandom : IRandomSource
{
    public int NextInclusive(int minInclusive, int maxInclusive) => maxInclusive;
}
