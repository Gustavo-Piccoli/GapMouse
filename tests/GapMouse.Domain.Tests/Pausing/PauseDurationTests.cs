using GapMouse.Domain.Pausing;

namespace GapMouse.Domain.Tests.Pausing;

public class PauseDurationTests
{
    [Fact]
    public void Presets_Always_HaveExpectedLengths()
    {
        PauseDuration.TenMinutes.Length.ShouldBe(TimeSpan.FromMinutes(10));
        PauseDuration.ThirtyMinutes.Length.ShouldBe(TimeSpan.FromMinutes(30));
        PauseDuration.OneHour.Length.ShouldBe(TimeSpan.FromHours(1));
        PauseDuration.Indefinite.IsIndefinite.ShouldBeTrue();
        PauseDuration.Indefinite.Length.ShouldBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1441)]
    public void Custom_OutOfRange_Throws(int minutes) =>
        Should.Throw<ArgumentOutOfRangeException>(() => PauseDuration.Custom(minutes));

    [Theory]
    [InlineData(1)]
    [InlineData(1440)]
    public void Custom_InRange_HasLength(int minutes) =>
        PauseDuration.Custom(minutes).Length.ShouldBe(TimeSpan.FromMinutes(minutes));
}
