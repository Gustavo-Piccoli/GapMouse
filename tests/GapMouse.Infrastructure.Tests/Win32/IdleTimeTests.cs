using GapMouse.Infrastructure.Win32;

namespace GapMouse.Infrastructure.Tests.Win32;

public class IdleTimeTests
{
    [Fact]
    public void FromTicks_Normal_ReturnsDifferenceInMilliseconds() =>
        IdleTime.FromTicks(10_000, 7_500).ShouldBe(TimeSpan.FromMilliseconds(2_500));

    [Fact]
    public void FromTicks_AcrossWrapAround_IsSmallPositive() =>
        IdleTime.FromTicks(5, uint.MaxValue - 4).ShouldBe(TimeSpan.FromMilliseconds(10));
}
