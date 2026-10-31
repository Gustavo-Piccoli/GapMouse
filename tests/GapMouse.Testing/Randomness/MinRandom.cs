using GapMouse.Domain.Randomness;

namespace GapMouse.Testing.Randomness;

/// <summary>Always returns the lower bound.</summary>
public sealed class MinRandom : IRandomSource
{
    public int NextInclusive(int minInclusive, int maxInclusive) => minInclusive;
}
