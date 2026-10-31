namespace GapMouse.Domain.Randomness;

/// <summary>Source of randomness, abstracted so behavior can be tested deterministically.</summary>
public interface IRandomSource
{
    /// <summary>Returns a random integer between the two bounds, both inclusive.</summary>
    int NextInclusive(int minInclusive, int maxInclusive);
}
