using GapMouse.Domain.Settings;

namespace GapMouse.Domain.Tests.Settings;

public class EngineSettingsTests
{
    [Fact]
    public void Default_Always_MatchesLegacyBatValues()
    {
        var s = EngineSettings.Default;
        s.IdleThreshold.ShouldBe(TimeSpan.FromSeconds(120));
        s.CheckInterval.ShouldBe(TimeSpan.FromSeconds(1));
        s.MovementIntervalMin.ShouldBe(TimeSpan.FromSeconds(10));
        s.MovementIntervalMax.ShouldBe(TimeSpan.FromSeconds(25));
        s.AmplitudePixels.ShouldBe(120);
        s.KeepDisplayOn.ShouldBeTrue();
    }

    [Theory]
    [InlineData(9, 1, 10, 25, 120, SettingsField.IdleThreshold)]
    [InlineData(3601, 1, 10, 25, 120, SettingsField.IdleThreshold)]
    [InlineData(120, 0, 10, 25, 120, SettingsField.CheckInterval)]
    [InlineData(120, 61, 10, 25, 120, SettingsField.CheckInterval)]
    [InlineData(120, 1, 0, 25, 120, SettingsField.MovementIntervalMin)]
    [InlineData(120, 1, 10, 3601, 120, SettingsField.MovementIntervalMax)]
    [InlineData(120, 1, 10, 25, 9, SettingsField.Amplitude)]
    [InlineData(120, 1, 10, 25, 501, SettingsField.Amplitude)]
    public void Create_OutOfRange_ReportsField(int idle, int check, int min, int max, int amp, SettingsField field)
    {
        var r = EngineSettings.Create(idle, check, min, max, amp, true);
        r.IsValid.ShouldBeFalse();
        r.Errors.ShouldContain(new SettingsError(field, SettingsErrorCode.OutOfRange));
    }

    [Fact]
    public void Create_MinGreaterThanMax_ReportsMinGreaterThanMax() =>
        EngineSettings.Create(120, 1, 30, 20, 120, true).Errors
            .ShouldContain(new SettingsError(SettingsField.MovementIntervalMin, SettingsErrorCode.MinGreaterThanMax));

    [Fact]
    public void Create_SeveralErrors_ReportsAll() =>
        EngineSettings.Create(0, 0, 10, 25, 0, true).Errors.Count.ShouldBe(3);

    [Fact]
    public void Create_MinEqualsMax_IsValid() =>
        EngineSettings.Create(120, 1, 15, 15, 120, false).IsValid.ShouldBeTrue();

    [Fact]
    public void Create_ValidValues_ExposesValue()
    {
        var s = EngineSettings.Create(300, 5, 10, 20, 200, false).Value;
        s.IdleThreshold.ShouldBe(TimeSpan.FromSeconds(300));
        s.CheckInterval.ShouldBe(TimeSpan.FromSeconds(5));
        s.AmplitudePixels.ShouldBe(200);
        s.KeepDisplayOn.ShouldBeFalse();
    }

    [Fact]
    public void Value_InvalidResult_Throws() =>
        Should.Throw<InvalidOperationException>(() => EngineSettings.Create(0, 1, 10, 25, 120, true).Value);

    [Fact]
    public void Default_KeepAwakeWhileLocked_IsFalse() =>
        EngineSettings.Default.KeepAwakeWhileLocked.ShouldBeFalse();

    [Fact]
    public void Create_DefaultsKeepAwakeWhileLockedToFalse() =>
        EngineSettings.Create(120, 1, 10, 25, 120, true).Value.KeepAwakeWhileLocked.ShouldBeFalse();

    [Fact]
    public void Create_SetsKeepAwakeWhileLocked() =>
        EngineSettings.Create(120, 1, 10, 25, 120, true, keepAwakeWhileLocked: true).Value.KeepAwakeWhileLocked.ShouldBeTrue();
}
