using GapMouse.Domain.Randomness;

namespace GapMouse.Infrastructure.Randomness;

public sealed class SystemRandomSource : IRandomSource
{
    public int NextInclusive(int minInclusive, int maxInclusive) =>
        Random.Shared.Next(minInclusive, maxInclusive + 1);
}
