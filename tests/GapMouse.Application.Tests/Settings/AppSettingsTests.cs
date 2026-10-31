using GapMouse.Application.Settings;
using GapMouse.Domain.Settings;

namespace GapMouse.Application.Tests.Settings;

public class AppSettingsTests
{
    [Fact]
    public void Default_MatchesSpecDefaults()
    {
        var s = AppSettings.Default;

        s.Engine.ShouldBe(EngineSettings.Default);
        s.StartWithWindows.ShouldBeFalse();
        s.StartMinimized.ShouldBeFalse();
        s.NotificationsEnabled.ShouldBeFalse();
        s.PauseHotkey.ShouldBe("Ctrl+Alt+P");
        s.CheckForUpdates.ShouldBeTrue();
        s.Theme.ShouldBe(AppTheme.Dark);
        s.Language.ShouldBe(UiLanguage.Auto);
        s.SaveLogsToFile.ShouldBeTrue();
        s.LogRetentionDays.ShouldBe(7);
        s.TrayHintShown.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(366)]
    [InlineData(-1)]
    public void LogRetentionDays_OutsideRange_Throws(int value) =>
        Should.Throw<ArgumentOutOfRangeException>(() => AppSettings.Default with { LogRetentionDays = value });

    [Theory]
    [InlineData(1)]
    [InlineData(365)]
    public void LogRetentionDays_AtBounds_Accepted(int value) =>
        (AppSettings.Default with { LogRetentionDays = value }).LogRetentionDays.ShouldBe(value);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void PauseHotkey_Blank_Throws(string value) =>
        Should.Throw<ArgumentException>(() => AppSettings.Default with { PauseHotkey = value });
}
