using GapMouse.Application.Logging;
using GapMouse.Application.Settings;
using GapMouse.Application.Shell;
using GapMouse.Application.Startup;
using GapMouse.Domain.Settings;
using GapMouse.Testing.Engine;
using GapMouse.Testing.Logging;
using Microsoft.Extensions.Logging;

namespace GapMouse.Application.Tests.Settings;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly FakeStore _store = new();
    private readonly FakeRegistrar _registrar = new();
    private readonly FakeLogOptions _logs = new();
    private readonly FakeHotkey _hotkey = new();
    private readonly FakeEngineController _engine = new();
    private readonly ListLogger<SettingsService> _logger = new();

    public void Dispose() => _hotkey.Dispose();

    private static EngineSettings OtherEngine() => EngineSettings.Create(60, 2, 5, 9, 50, false).Value;

    [Fact]
    public void Initialize_FirstLaunchWithRunKey_TurnsStartWithWindowsOn()
    {
        _registrar.Command = "\"x.exe\" --minimized";
        var service = Create(new SettingsLoadResult(AppSettings.Default, FileExisted: false, []));

        service.Initialize();

        service.Current.StartWithWindows.ShouldBeTrue();
    }

    [Fact]
    public void Initialize_InvalidHotkeyText_WarnsAndNotRegistered()
    {
        var settings = AppSettings.Default with { PauseHotkey = "nonsense" };
        var service = Create(new SettingsLoadResult(settings, true, []));

        service.Initialize();

        service.IsHotkeyRegistered.ShouldBeFalse();
        _logger.Levels.ShouldContain(LogLevel.Warning);
        _hotkey.Registered.ShouldBeEmpty();
    }

    [Fact]
    public void Initialize_HotkeyTaken_WarnsAndNotRegistered()
    {
        _hotkey.Accept = false;
        var service = Create(new SettingsLoadResult(AppSettings.Default, true, []));

        service.Initialize();

        service.IsHotkeyRegistered.ShouldBeFalse();
        _logger.Levels.ShouldContain(LogLevel.Warning);
    }

    [Fact]
    public void Save_EngineChanged_AppliesToEngine()
    {
        var service = Initialized();
        var engine = OtherEngine();

        service.Save(AppSettings.Default with { Engine = engine });

        _engine.ApplySettingsCalls.ShouldBe([engine]);
    }

    [Fact]
    public void Save_EngineUnchanged_DoesNotApplyToEngine()
    {
        var service = Initialized();

        service.Save(AppSettings.Default with { NotificationsEnabled = true });

        _engine.ApplySettingsCalls.ShouldBeEmpty();
    }

    [Fact]
    public void Save_ConfiguresLogsAndStartup()
    {
        var service = Initialized();

        var result = service.Save(AppSettings.Default with { SaveLogsToFile = false, LogRetentionDays = 3, StartWithWindows = true });

        _logs.Calls.Last().ShouldBe((false, 3));
        _registrar.Command.ShouldNotBeNull();
        result.StartupApplied.ShouldBeTrue();
    }

    [Fact]
    public void Save_NewHotkey_ReRegisters()
    {
        var service = Initialized();

        var result = service.Save(AppSettings.Default with { PauseHotkey = "Ctrl+Alt+K" });

        _hotkey.Registered.Count.ShouldBe(2);
        _hotkey.Registered.Last().ToString().ShouldBe("Ctrl+Alt+K");
        result.HotkeyRegistered.ShouldBeTrue();
    }

    [Fact]
    public void Save_StoreThrows_ReturnsNotSavedButKeepsSettingsInSession()
    {
        var service = Initialized();
        _store.Throw = new UnauthorizedAccessException();
        var settings = AppSettings.Default with { NotificationsEnabled = true };

        var result = service.Save(settings);

        result.Saved.ShouldBeFalse();
        service.Current.ShouldBe(settings);
        _logger.Levels.ShouldContain(LogLevel.Warning);
    }

    [Fact]
    public void Save_RaisesChanged()
    {
        var service = Initialized();
        AppSettings? received = null;
        service.Changed += (_, s) => received = s;
        var settings = AppSettings.Default with { StartMinimized = true };

        service.Save(settings);

        received.ShouldBe(settings);
        _store.Saved.ShouldBe([settings]);
    }

    [Fact]
    public void MarkTrayHintShown_SavesWithFlag()
    {
        var service = Initialized();

        service.MarkTrayHintShown();

        service.Current.TrayHintShown.ShouldBeTrue();
        _store.Saved.Single().TrayHintShown.ShouldBeTrue();
    }

    [Fact]
    public void MarkTrayHintShown_AfterReadFailure_DoesNotSave()
    {
        var service = Create(new SettingsLoadResult(AppSettings.Default, true, []) { ReadFailed = true });
        service.Initialize();

        service.MarkTrayHintShown();

        service.Current.TrayHintShown.ShouldBeTrue();
        _store.Saved.ShouldBeEmpty();
    }

    [Fact]
    public void Save_AfterReadFailure_SavesAndLaterHintSaves()
    {
        var service = Create(new SettingsLoadResult(AppSettings.Default, true, []) { ReadFailed = true });
        service.Initialize();

        var result = service.Save(AppSettings.Default with { StartMinimized = true });
        service.MarkTrayHintShown();

        result.Saved.ShouldBeTrue();
        _store.Saved.Count.ShouldBe(2);
        _store.Saved[1].TrayHintShown.ShouldBeTrue();
    }

    private SettingsService Initialized()
    {
        var service = Create(new SettingsLoadResult(AppSettings.Default, true, []));
        service.Initialize();
        return service;
    }

    private SettingsService Create(SettingsLoadResult loaded) => new(
        loaded,
        _store,
        _engine,
        _logs,
        new StartWithWindowsService(_registrar, @"C:\Apps\GapMouse.exe", new ListLogger<StartWithWindowsService>()),
        _hotkey,
        _logger);

    private sealed class FakeStore : ISettingsStore
    {
        public List<AppSettings> Saved { get; } = [];

        public Exception? Throw { get; set; }

        public SettingsLoadResult Load() => new(AppSettings.Default, true, []);

        public void Save(AppSettings settings)
        {
            if (Throw is not null)
            {
                throw Throw;
            }

            Saved.Add(settings);
        }
    }

    private sealed class FakeRegistrar : IStartupRegistrar
    {
        public string? Command { get; set; }

        public string? GetCommand() => Command;

        public void SetCommand(string command) => Command = command;

        public void Remove() => Command = null;
    }

    private sealed class FakeLogOptions : ILogFileOptions
    {
        public List<(bool Enabled, int Days)> Calls { get; } = [];

        public void Configure(bool enabled, int retentionDays) => Calls.Add((enabled, retentionDays));
    }

    private sealed class FakeHotkey : IGlobalHotkey
    {
        public bool Accept { get; set; } = true;

        public List<HotkeyGesture> Registered { get; } = [];

        public event EventHandler? Pressed
        {
            add { }
            remove { }
        }

        public bool TryRegister(HotkeyGesture gesture)
        {
            Registered.Add(gesture);
            return Accept;
        }

        public void Unregister()
        {
        }

        public void Dispose()
        {
        }
    }
}
