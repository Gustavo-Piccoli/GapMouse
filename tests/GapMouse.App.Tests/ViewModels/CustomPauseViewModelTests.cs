using GapMouse.App.Localization;
using GapMouse.App.ViewModels;

namespace GapMouse.App.Tests.ViewModels;

public sealed class CustomPauseViewModelTests
{
    [Theory]
    [InlineData("1", 1)]
    [InlineData("1440", 1440)]
    public void MinutesText_Valid_ParsesMinutes(string text, int expected)
    {
        var vm = new CustomPauseViewModel(new Localizer()) { MinutesText = text };
        vm.Minutes.ShouldBe(expected);
        vm.Error.ShouldBeNull();
        vm.CanConfirm.ShouldBeTrue();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1441")]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("2.5")]
    public void MinutesText_Invalid_HasErrorAndCannotConfirm(string text)
    {
        var vm = new CustomPauseViewModel(new Localizer()) { MinutesText = text };
        vm.Minutes.ShouldBeNull();
        vm.Error.ShouldNotBeNull();
        vm.CanConfirm.ShouldBeFalse();
    }
}
