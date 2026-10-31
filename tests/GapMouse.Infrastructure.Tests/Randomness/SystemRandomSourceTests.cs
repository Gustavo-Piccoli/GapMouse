using GapMouse.Infrastructure.Randomness;

namespace GapMouse.Infrastructure.Tests.Randomness;

public class SystemRandomSourceTests
{
    [Fact]
    public void NextInclusive_CanReturnBothBounds()
    {
        var random = new SystemRandomSource();

        var seen = Enumerable.Range(0, 2000).Select(_ => random.NextInclusive(10, 12)).ToHashSet();

        seen.ShouldBe(new HashSet<int> { 10, 11, 12 }, ignoreOrder: true);
    }
}
