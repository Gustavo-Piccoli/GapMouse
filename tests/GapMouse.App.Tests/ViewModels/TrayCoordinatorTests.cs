using GapMouse.App.Localization;
using GapMouse.App.Services;
using GapMouse.App.Tests.Fakes;
using GapMouse.App.ViewModels;
using GapMouse.Application.Settings;
using GapMouse.Application.Shell;
using GapMouse.Application.Updates;
using GapMouse.Domain.Engine;
using GapMouse.Domain.Pausing;
using GapMouse.Testing.Engine;

namespace GapMouse.App.Tests.ViewModels;

public sealed class TrayCoordinatorTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly FakeEngineController _engine = new();
    private readonly FakeTray _tray = new();
    private readonly FakeHotkey _hotkey = new();
    private readonly FakeSettings _settings = new();
    private readonly FakeUpdates _updates = new();
    private readonly FakePrompt _prompt = new();
    private readonly FakeWindow _window = new();
    private readonly FakeLifetime _lifetime = new();
    private readonly FakeLauncher _launcher = new();
    private readonly Localizer _l = new(() => System.Globalization.CultureInfo.GetCultureInfo("en-US"));

    public void Dispose()
    {
        _tray.Dispose();
        _hotkey.Dispose();
    }

    private TrayCoordinator Create()
    {
        var c = new TrayCoordinator(_l, _engine, _tray, _hotkey, _settings, _updates, _prompt, _window, _lifetime, _launcher, new ImmediateUiDispatcher());
        c.Start();
        return c;
    }

    private void Moving() => _engine.Raise(new MovingStarted(Now, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)));

    private void Paused() => _engine.Raise(new EnginePaused(Now, PauseDuration.Indefinite, null));

    [Fact]
    public void Start_SetsWaitingIconAndTooltip()
    {
        using var c = Create();

        _tray.States.ShouldBe([(TrayIconState.Waiting, "GapMouse — Waiting")]);
    }

    [Fact]
    public void MovingStarted_SwitchesToMovingIcon()
    {
        using var c = Create();

        Moving();

        _tray.States[^1].ShouldBe((TrayIconState.Moving, "GapMouse — Moving"));
    }

    [Fact]
    public void Paused_SwitchesToPausedIcon()
    {
        using var c = Create();

        Paused();

        _tray.States[^1].ShouldBe((TrayIconState.Paused, "GapMouse — Paused"));
    }

    [Fact]
    public void NotificationsOff_NoBalloonOnStateChange()
    {
        using var c = Create();

        Moving();

        _tray.Balloons.ShouldBeEmpty();
    }

    [Fact]
    public void NotificationsOn_MovingStarted_ShowsSilentBalloon()
    {
        _settings.Current = new AppSettings { NotificationsEnabled = true };
        using var c = Create();

        Moving();

        _tray.Balloons.ShouldBe([("GapMouse", "Keeping your PC awake", (string?)null, true)]);
    }

    [Fact]
    public void NotificationsOn_OtherEvents_ShowPhrases()
    {
        _settings.Current = new AppSettings { NotificationsEnabled = true };
        using var c = Create();

        _engine.Raise(new UserReturned(Now));
        Paused();
        _engine.Raise(new EngineResumed(Now, ResumeReason.Manual));

        _tray.Balloons.Select(b => b.Message).ShouldBe(["Welcome back", "Paused", "Resumed"]);
    }

    [Fact]
    public void Menu_WhenRunning_HasPauseSubmenuOpenExit()
    {
        using var c = Create();

        _tray.RaiseMenu();

        var items = _tray.MenuShown!;
        items.Select(i => i.Id).ShouldBe(["pause", "-", "open", "exit"]);
        items[0].Text.ShouldBe("Pause");
        items[0].Children!.Select(i => i.Id).ShouldBe(
            ["pause:TenMinutes", "pause:ThirtyMinutes", "pause:OneHour", "pause:Custom", "pause:Never"]);
        items[0].Children!.Select(i => i.Text).ShouldBe(["10 min", "30 min", "1 h", "Custom…", "Never"]);
        items[2].Text.ShouldBe("Open");
        items[3].Text.ShouldBe("Exit");
    }

    [Fact]
    public void Menu_WhenPaused_HasResume()
    {
        using var c = Create();
        Paused();

        _tray.RaiseMenu();

        _tray.MenuShown!.Select(i => i.Id).ShouldBe(["resume", "-", "open", "exit"]);
        _tray.MenuShown![0].Text.ShouldBe("Resume");
    }

    [Fact]
    public void Menu_ChooseResume_Resumes()
    {
        using var c = Create();
        Paused();
        _tray.Choice = "resume";

        _tray.RaiseMenu();

        _engine.ResumeCalls.ShouldBe(1);
    }

    [Fact]
    public void Menu_ChooseThirtyMinutes_PausesThirty()
    {
        using var c = Create();
        _tray.Choice = "pause:ThirtyMinutes";

        _tray.RaiseMenu();

        _engine.PauseCalls.ShouldBe([PauseDuration.ThirtyMinutes]);
    }

    [Fact]
    public void Menu_ChooseCustom_UsesPrompt()
    {
        using var c = Create();
        _prompt.Minutes = 42;
        _tray.Choice = "pause:Custom";

        _tray.RaiseMenu();

        _engine.PauseCalls.ShouldBe([PauseDuration.Custom(42)]);
    }

    [Fact]
    public void Menu_ChooseCustomCancelled_DoesNotPause()
    {
        using var c = Create();
        _prompt.Minutes = null;
        _tray.Choice = "pause:Custom";

        _tray.RaiseMenu();

        _engine.PauseCalls.ShouldBeEmpty();
    }

    [Fact]
    public void Menu_ChooseOpen_ShowsWindow()
    {
        using var c = Create();
        _tray.Choice = "open";

        _tray.RaiseMenu();

        _window.ShowCalls.ShouldBe(1);
    }

    [Fact]
    public void Menu_ChooseExit_CallsLifetime()
    {
        using var c = Create();
        _tray.Choice = "exit";

        _tray.RaiseMenu();

        _lifetime.ExitCalls.ShouldBe(1);
    }

    [Fact]
    public void DoubleClick_ShowsWindow()
    {
        using var c = Create();

        _tray.RaiseOpen();

        _window.ShowCalls.ShouldBe(1);
    }

    [Fact]
    public void Hotkey_WhenRunning_PausesIndefinitely()
    {
        using var c = Create();

        _hotkey.Raise();

        _engine.PauseCalls.ShouldBe([PauseDuration.Indefinite]);
        _engine.ResumeCalls.ShouldBe(0);
    }

    [Fact]
    public void Hotkey_WhenPaused_Resumes()
    {
        using var c = Create();
        Paused();

        _hotkey.Raise();

        _engine.ResumeCalls.ShouldBe(1);
        _engine.PauseCalls.ShouldBeEmpty();
    }

    [Fact]
    public void UpdateFound_ShowsBalloonOncePerVersion()
    {
        using var c = Create();
        var result = new UpdateCheckResult(UpdateCheckOutcome.UpdateAvailable, new Version(1, 2, 0), new Uri("https://example.com/r"));

        _updates.Raise(result);
        _updates.Raise(result);
        _updates.Raise(result with { LatestVersion = new Version(1, 3, 0) });

        _tray.Balloons.Count.ShouldBe(2);
        _tray.Balloons[0].ShouldBe(("Update available", "Version 1.2.0 is available.", "update:https://example.com/r", false));
    }

    [Fact]
    public void UpdateBalloonClicked_OpensReleaseUrl()
    {
        using var c = Create();

        _tray.RaiseBalloonClicked("update:https://example.com/r");
        _tray.RaiseBalloonClicked("other");
        _tray.RaiseBalloonClicked(null);

        _launcher.Opened.ShouldBe([new Uri("https://example.com/r")]);
    }

    [Fact]
    public void FirstWindowHide_ShowsHintAndMarksShown()
    {
        using var c = Create();

        c.OnMainWindowHidden();

        _tray.Balloons.ShouldBe([("GapMouse", "GapMouse is still running in the tray", (string?)null, true)]);
        _settings.MarkCalls.ShouldBe(1);
    }

    [Fact]
    public void LaterWindowHide_NoHint()
    {
        _settings.Current = new AppSettings { TrayHintShown = true };
        using var c = Create();

        c.OnMainWindowHidden();

        _tray.Balloons.ShouldBeEmpty();
        _settings.MarkCalls.ShouldBe(0);
    }

    [Fact]
    public void Portuguese_MenuTexts()
    {
        _l.SetLanguage(UiLanguage.Portuguese);
        using var c = Create();

        _tray.RaiseMenu();

        var items = _tray.MenuShown!;
        items[0].Text.ShouldBe("Pausar");
        items[0].Children!.Select(i => i.Text).ShouldBe(["10 min", "30 min", "1 h", "Personalizado…", "Nunca"]);
        items[2].Text.ShouldBe("Abrir");
        items[3].Text.ShouldBe("Sair");
        _tray.States[0].Tooltip.ShouldBe("GapMouse — Aguardando");
    }

    [Fact]
    public void LanguageChange_RefreshesTooltip()
    {
        using var c = Create();

        _l.SetLanguage(UiLanguage.Portuguese);

        _tray.States[^1].ShouldBe((TrayIconState.Waiting, "GapMouse — Aguardando"));
    }

    [Fact]
    public void Dispose_UnsubscribesFromEngine()
    {
        var c = Create();
        c.Dispose();

        Moving();

        _tray.States.Count.ShouldBe(1);
    }

    private sealed class FakeTray : ITrayIcon
    {
        public List<(TrayIconState State, string Tooltip)> States { get; } = [];

        public List<(string Title, string Message, string? Tag, bool Silent)> Balloons { get; } = [];

        public IReadOnlyList<TrayMenuItem>? MenuShown { get; private set; }

        public string? Choice { get; set; }

        public event EventHandler? OpenRequested;

        public event EventHandler? MenuRequested;

        public event EventHandler<string?>? BalloonClicked;

        public void SetState(TrayIconState state, string tooltip) => States.Add((state, tooltip));

        public void ShowBalloon(string title, string message, string? tag, bool silent) =>
            Balloons.Add((title, message, tag, silent));

        public string? ShowMenu(IReadOnlyList<TrayMenuItem> items)
        {
            MenuShown = items;
            return Choice;
        }

        public void RaiseMenu() => MenuRequested?.Invoke(this, EventArgs.Empty);

        public void RaiseOpen() => OpenRequested?.Invoke(this, EventArgs.Empty);

        public void RaiseBalloonClicked(string? tag) => BalloonClicked?.Invoke(this, tag);

        public void Dispose()
        {
        }
    }

    private sealed class FakeHotkey : IGlobalHotkey
    {
        public event EventHandler? Pressed;

        public bool TryRegister(HotkeyGesture gesture) => true;

        public void Unregister()
        {
        }

        public void Raise() => Pressed?.Invoke(this, EventArgs.Empty);

        public void Dispose()
        {
        }
    }

    private sealed class FakeSettings : ISettingsService
    {
        public AppSettings Current { get; set; } = new();

        public bool IsHotkeyRegistered => true;

        public int MarkCalls { get; private set; }

        public event EventHandler<AppSettings>? Changed
        {
            add { }
            remove { }
        }

        public void Initialize()
        {
        }

        public SettingsSaveResult Save(AppSettings settings) => throw new NotSupportedException();

        public void MarkTrayHintShown() => MarkCalls++;
    }

    private sealed class FakeUpdates : IUpdateChecker
    {
        public event EventHandler<UpdateCheckResult>? UpdateFound;

        public Task<UpdateCheckResult> CheckNowAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public void Raise(UpdateCheckResult result) => UpdateFound?.Invoke(this, result);
    }

    private sealed class FakePrompt : IPausePrompt
    {
        public int? Minutes { get; set; }

        public Task<int?> AskMinutesAsync() => Task.FromResult(Minutes);
    }

    private sealed class FakeWindow : IMainWindowService
    {
        public int ShowCalls { get; private set; }

        public void Show() => ShowCalls++;
    }

    private sealed class FakeLifetime : IAppLifetime
    {
        public int ExitCalls { get; private set; }

#pragma warning disable CA1716 // Mirrors the interface contract.
        public void Exit() => ExitCalls++;
#pragma warning restore CA1716
    }

    private sealed class FakeLauncher : IShellLauncher
    {
        public List<Uri> Opened { get; } = [];

        public void OpenFolder(string path)
        {
        }

        public void OpenUrl(Uri url) => Opened.Add(url);
    }
}
