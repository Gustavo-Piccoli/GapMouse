using GapMouse.App.Services;
using GapMouse.Domain.Pausing;

namespace GapMouse.App.Tests.Services;

public sealed class PauseOptionTests
{
    [Theory]
    [InlineData(PauseOption.TenMinutes, 10)]
    [InlineData(PauseOption.ThirtyMinutes, 30)]
    [InlineData(PauseOption.OneHour, 60)]
    public void ToDuration_FixedOption_MapsToMatchingDuration(PauseOption option, int minutes) =>
        PauseOptions.ToDuration(option, null).Length.ShouldBe(TimeSpan.FromMinutes(minutes));

    [Fact]
    public void ToDuration_Never_IsIndefinite() =>
        PauseOptions.ToDuration(PauseOption.Never, null).ShouldBe(PauseDuration.Indefinite);

    [Fact]
    public void ToDuration_CustomWithMinutes_UsesThem() =>
        PauseOptions.ToDuration(PauseOption.Custom, 45).Length.ShouldBe(TimeSpan.FromMinutes(45));

    [Fact]
    public void ToDuration_CustomWithoutMinutes_Throws() =>
        Should.Throw<ArgumentException>(() => PauseOptions.ToDuration(PauseOption.Custom, null));
}
