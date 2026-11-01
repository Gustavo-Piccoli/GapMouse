using GapMouse.App.Localization;
using GapMouse.App.ViewModels;
using GapMouse.Application.Settings;
using GapMouse.Application.Shell;
using GapMouse.Domain.Settings;

namespace GapMouse.App.Tests.ViewModels;

public sealed class SettingsViewModelTests
{
    private static Localizer English()
    {
        var l = new Localizer(() => new System.Globalization.CultureInfo("en-US"));
        l.SetLanguage(UiLanguage.English);
        return l;
    }

    private static (SettingsViewModel Vm, FakeSettingsService Service) Create(Localizer? l = null)
    {
        var service = new FakeSettingsService();
        return (new SettingsViewModel(l ?? English(), service), service);
    }

    [Fact]
    public void Load_ShowsCurrentValues()
    {
        var (vm, _) = Create();

        vm.IdleText.ShouldBe("120");
        vm.CheckText.ShouldBe("1");
        vm.MinText.ShouldBe("10");
        vm.MaxText.ShouldBe("25");
        vm.AmplitudeText.ShouldBe("120");
        vm.RetentionText.ShouldBe("7");
        vm.HotkeyText.ShouldBe("Ctrl+Alt+P");
        vm.KeepDisplayOn.ShouldBeTrue();
        vm.IsDirty.ShouldBeFalse();
        vm.HasErrors.ShouldBeFalse();
        vm.SaveCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public void MinGreaterThanMax_ShowsErrorAndDisablesSave()
    {
        var (vm, _) = Create();

        vm.MinText = "30";

        vm.MinError.ShouldNotBeNull();
        vm.HasErrors.ShouldBeTrue();
        vm.SaveCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public void EmptyIdle_ShowsError()
    {
        var (vm, _) = Create();

        vm.IdleText = string.Empty;

        vm.IdleError.ShouldBe("Enter a whole number.");
        vm.MinError.ShouldBeNull();
    }

    [Fact]
    public void Letters_ShowError()
    {
        var (vm, _) = Create();

        vm.AmplitudeText = "abc";

        vm.AmplitudeError.ShouldBe("Enter a whole number.");
    }

    [Fact]
    public void OutOfRange_ShowsRangeFromDomain()
    {
        var (vm, _) = Create();

        vm.CheckText = "61";

        vm.CheckError.ShouldBe("Enter a value from 1 to 60.");
    }

    [Fact]
    public void EngineRanges_MatchDomainBoundaries()
    {
        foreach (var (field, (min, max)) in SettingsViewModel.EngineRanges)
        {
            Errors(field, min).ShouldNotContain(e => e.Field == field && e.Code == SettingsErrorCode.OutOfRange);
            Errors(field, max).ShouldNotContain(e => e.Field == field && e.Code == SettingsErrorCode.OutOfRange);
            Errors(field, min - 1).ShouldContain(e => e.Field == field && e.Code == SettingsErrorCode.OutOfRange);
            Errors(field, max + 1).ShouldContain(e => e.Field == field && e.Code == SettingsErrorCode.OutOfRange);
        }

        static IReadOnlyList<SettingsError> Errors(SettingsField field, int value)
        {
            int[] v = [120, 1, 10, 25, 120];
            v[(int)field] = value;
            return EngineSettings.Create(v[0], v[1], v[2], v[3], v[4], true).Errors;
        }
    }

    [Fact]
    public void Retention0_ShowsError()
    {
        var (vm, _) = Create();

        vm.RetentionText = "0";

        vm.RetentionError.ShouldBe("Enter a value from 1 to 365.");
    }

    [Fact]
    public void InvalidHotkeyText_ShowsError()
    {
        var (vm, _) = Create();

        vm.HotkeyText = "P";

        vm.HotkeyError.ShouldNotBeNull();
        vm.SaveCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public void CaptureHotkey_SetsCanonicalText()
    {
        var (vm, _) = Create();

        vm.CaptureHotkey(HotkeyModifiers.Control | HotkeyModifiers.Alt, "K");

        vm.HotkeyText.ShouldBe("Ctrl+Alt+K");
        vm.HotkeyError.ShouldBeNull();
        vm.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public void CaptureHotkey_NoModifier_IsIgnored()
    {
        var (vm, _) = Create();

        vm.CaptureHotkey(HotkeyModifiers.None, "K");

        vm.HotkeyText.ShouldBe("Ctrl+Alt+P");
        vm.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void CaptureHotkey_ShiftOnly_IsIgnored()
    {
        var (vm, _) = Create();

        vm.CaptureHotkey(HotkeyModifiers.Shift, "K");

        vm.HotkeyText.ShouldBe("Ctrl+Alt+P");
    }

    [Theory]
    [InlineData("Escape")]
    [InlineData("")]
    [InlineData("F25")]
    [InlineData("KK")]
    public void CaptureHotkey_InvalidKey_IsIgnored(string key)
    {
        var (vm, _) = Create();

        vm.CaptureHotkey(HotkeyModifiers.Control, key);

        vm.HotkeyText.ShouldBe("Ctrl+Alt+P");
        vm.IsDirty.ShouldBeFalse();
    }

    [Theory]
    [InlineData("en-US", "1,5")]
    [InlineData("en-US", "1.5")]
    [InlineData("pt-BR", "1,5")]
    [InlineData("pt-BR", "1.5")]
    public void DecimalValue_IsRejectedInEveryNumericField(string culture, string text)
    {
        var l = new Localizer(() => new System.Globalization.CultureInfo(culture));
        l.SetLanguage(culture == "pt-BR" ? UiLanguage.Portuguese : UiLanguage.English);
        var (vm, service) = Create(l);

        foreach (var set in Setters(vm))
        {
            set(text);
        }

        AllErrors(vm).ShouldAllBe(e => e != null);
        vm.SaveCommand.Execute(null);
        service.Saved.ShouldBeEmpty();
    }

    [Fact]
    public void NegativeValue_ShowsRangeError()
    {
        var (vm, _) = Create();

        vm.IdleText = "-5";
        vm.RetentionText = "-1";

        vm.IdleError.ShouldBe("Enter a value from 10 to 3600.");
        vm.RetentionError.ShouldBe("Enter a value from 1 to 365.");
    }

    [Fact]
    public void RetentionAboveMax_ShowsError()
    {
        var (vm, _) = Create();

        vm.RetentionText = "366";

        vm.RetentionError.ShouldBe("Enter a value from 1 to 365.");
    }

    [Fact]
    public void EmptyHotkey_ShowsError()
    {
        var (vm, _) = Create();

        vm.HotkeyText = string.Empty;

        vm.HotkeyError.ShouldNotBeNull();
        vm.SaveCommand.CanExecute(null).ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    public void EmptyOrNonNumeric_ShowsErrorInEachNumericField(string text)
    {
        foreach (var index in Enumerable.Range(0, 6))
        {
            var (vm, _) = Create();

            Setters(vm)[index](text);

            AllErrors(vm)[index].ShouldBe("Enter a whole number.");
            AllErrors(vm).Count(e => e != null).ShouldBe(1);
        }
    }

    [Fact]
    public void Save_InvalidForm_NeverCallsService()
    {
        var (vm, service) = Create();
        vm.MinText = "30";

        vm.SaveCommand.Execute(null);

        service.Saved.ShouldBeEmpty();
        vm.StatusMessage.ShouldBeNull();
    }

    private static Action<string>[] Setters(SettingsViewModel vm) =>
    [
        v => vm.IdleText = v,
        v => vm.CheckText = v,
        v => vm.MinText = v,
        v => vm.MaxText = v,
        v => vm.AmplitudeText = v,
        v => vm.RetentionText = v,
    ];

    private static string?[] AllErrors(SettingsViewModel vm) =>
        [vm.IdleError, vm.CheckError, vm.MinError, vm.MaxError, vm.AmplitudeError, vm.RetentionError];

    [Fact]
    public void KeepAwakeWhileLocked_DefaultsOffAndToggleMarksDirty()
    {
        var (vm, _) = Create();

        vm.KeepAwakeWhileLocked.ShouldBeFalse();
        vm.IsDirty.ShouldBeFalse();

        vm.KeepAwakeWhileLocked = true;

        vm.IsDirty.ShouldBeTrue();
        vm.SaveCommand.CanExecute(null).ShouldBeTrue();
    }

    [Fact]
    public void KeepAwakeWhileLocked_SavedIntoEngineSettings()
    {
        var (vm, service) = Create();
        vm.KeepAwakeWhileLocked = true;

        vm.SaveCommand.Execute(null);

        service.Saved.ShouldHaveSingleItem().Engine.KeepAwakeWhileLocked.ShouldBeTrue();
        vm.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void KeepAwakeWhileLocked_LoadedFromStoredSettings()
    {
        var service = new FakeSettingsService
        {
            Current = AppSettings.Default with
            {
                Engine = EngineSettings.Create(120, 1, 10, 25, 120, true, true).Value,
            },
        };

        var vm = new SettingsViewModel(English(), service);

        vm.KeepAwakeWhileLocked.ShouldBeTrue();
        vm.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void Save_ValidForm_CallsServiceWithParsedSettings()
    {
        var (vm, service) = Create();
        vm.IdleText = "300";
        vm.MaxText = "40";
        vm.HotkeyText = "ctrl+alt+k";
        vm.RetentionText = "14";
        vm.Theme = AppTheme.Light;

        vm.SaveCommand.CanExecute(null).ShouldBeTrue();
        vm.SaveCommand.Execute(null);

        var saved = service.Saved.ShouldHaveSingleItem();
        saved.Engine.IdleThreshold.ShouldBe(TimeSpan.FromSeconds(300));
        saved.Engine.MovementIntervalMax.ShouldBe(TimeSpan.FromSeconds(40));
        saved.PauseHotkey.ShouldBe("Ctrl+Alt+K");
        saved.LogRetentionDays.ShouldBe(14);
        saved.Theme.ShouldBe(AppTheme.Light);
        vm.StatusMessage.ShouldBe("Settings saved.");
        vm.IsDirty.ShouldBeFalse();
        vm.HotkeyText.ShouldBe("Ctrl+Alt+K");
    }

    [Fact]
    public void Save_NotSaved_ShowsDiskMessage()
    {
        var (vm, service) = Create();
        service.Result = new SettingsSaveResult(false, true, true);
        vm.IdleText = "300";

        vm.SaveCommand.Execute(null);

        vm.StatusMessage.ShouldBe("Applied, but the settings file could not be written.");
    }

    [Fact]
    public void Save_StartupFailed_ShowsStartupMessage()
    {
        var (vm, service) = Create();
        service.Result = new SettingsSaveResult(true, false, true);
        vm.IdleText = "300";

        vm.SaveCommand.Execute(null);

        vm.StatusMessage.ShouldBe("Saved, but start with Windows could not be changed.");
    }

    [Fact]
    public void Save_HotkeyFailed_ShowsHotkeyWarning()
    {
        var (vm, service) = Create();
        service.Result = new SettingsSaveResult(true, true, false);
        vm.IdleText = "300";

        vm.SaveCommand.Execute(null);

        vm.StatusMessage.ShouldBe("Shortcut unavailable — another app may be using it.");
    }

    [Fact]
    public void Save_ChangedRaisedByOwnSave_KeepsStatusMessage()
    {
        var (vm, service) = Create();
        vm.IdleText = "300";
        vm.SaveCommand.Execute(null);

        service.RaiseChanged();

        vm.StatusMessage.ShouldBe("Settings saved.");
    }

    [Fact]
    public void RestoreDefaults_FillsDefaultsAndIsDirtyWithoutSaving()
    {
        var (vm, service) = Create();
        service.Current = AppSettings.Default with { LogRetentionDays = 30 };
        service.RaiseChanged();
        vm.RetentionText.ShouldBe("30");

        vm.RestoreDefaultsCommand.Execute(null);

        vm.RetentionText.ShouldBe("7");
        vm.IsDirty.ShouldBeTrue();
        service.Saved.ShouldBeEmpty();
    }

    [Fact]
    public void HotkeyNotRegistered_ShowsWarning()
    {
        var service = new FakeSettingsService { IsHotkeyRegistered = false };

        var vm = new SettingsViewModel(English(), service);

        vm.HotkeyWarning.ShouldBe("Shortcut unavailable — another app may be using it.");
    }

    [Fact]
    public void HotkeyRegistered_HasNoWarning()
    {
        var (vm, _) = Create();

        vm.HotkeyWarning.ShouldBeNull();
    }

    [Fact]
    public void ExternalChange_ReloadsWhenNotDirty()
    {
        var (vm, service) = Create();
        service.Current = AppSettings.Default with { LogRetentionDays = 20 };

        service.RaiseChanged();

        vm.RetentionText.ShouldBe("20");
    }

    [Fact]
    public void ExternalChange_DoesNotOverwriteDirtyForm()
    {
        var (vm, service) = Create();
        vm.IdleText = "300";
        service.Current = AppSettings.Default with { LogRetentionDays = 20 };

        service.RaiseChanged();

        vm.IdleText.ShouldBe("300");
        vm.RetentionText.ShouldBe("7");
    }

    [Fact]
    public void Portuguese_ErrorMessagesInPortuguese()
    {
        var l = English();
        var (vm, _) = Create(l);
        vm.IdleText = "x";
        vm.RetentionText = "0";

        l.SetLanguage(UiLanguage.Portuguese);

        vm.IdleError.ShouldBe("Digite um número inteiro.");
        vm.RetentionError.ShouldBe("Digite um valor de 1 a 365.");
    }

    [Fact]
    public void Dispose_UnsubscribesFromChanged()
    {
        var (vm, service) = Create();
        vm.Dispose();
        service.Current = AppSettings.Default with { LogRetentionDays = 20 };

        service.RaiseChanged();

        vm.RetentionText.ShouldBe("7");
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public List<AppSettings> Saved { get; } = [];

        public SettingsSaveResult Result { get; set; } = new(true, true, true);

        public AppSettings Current { get; set; } = AppSettings.Default;

        public bool IsHotkeyRegistered { get; set; } = true;

        public event EventHandler<AppSettings>? Changed;

        public void Initialize()
        {
        }

        public SettingsSaveResult Save(AppSettings settings)
        {
            Saved.Add(settings);
            Current = settings;
            Changed?.Invoke(this, settings);
            return Result;
        }

        public void MarkTrayHintShown()
        {
        }

        public void RaiseChanged() => Changed?.Invoke(this, Current);
    }
}
