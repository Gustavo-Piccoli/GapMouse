using GapMouse.App.Localization;
using GapMouse.App.ViewModels;
using GapMouse.Application.Logging;
using GapMouse.Application.Paths;
using GapMouse.Application.Settings;
using GapMouse.Domain.Engine;
using GapMouse.Testing.Engine;
using Microsoft.Extensions.Time.Testing;

namespace GapMouse.Screenshots;

/// <summary>One fully wired set of view models for a screenshot, built from test doubles.</summary>
internal sealed record Scene(Localizer Localizer, MainWindowViewModel Main, IDisposable[] Disposables);

internal static class Scenarios
{
    public static readonly string[] Names =
        ["home-waiting", "home-moving", "home-paused", "logs", "logs-empty", "settings", "settings-status", "about", "custom-pause"];

    // The fake clock uses UTC as its local zone, so every offset below is zero.
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 14, 32, 10, TimeSpan.Zero);

    public static int TabIndex(string scenario) => scenario switch
    {
        "home-waiting" or "home-moving" or "home-paused" => 0,
        "logs" or "logs-empty" => 1,
        "settings" or "settings-status" => 2,
        "about" => 3,
        _ => 0,
    };

    public static Scene Build(string scenario, UiLanguage language)
    {
        var l = new Localizer();
        l.SetLanguage(language);

        var time = new FakeTimeProvider(Now);
        time.SetLocalTimeZone(TimeZoneInfo.Utc);

        var engine = new FakeEngineController { Snapshot = SnapshotFor(scenario) };
        var ui = new ImmediateUiDispatcher();
        var settings = new InMemorySettingsService { FailToWrite = scenario == "settings-status" };
        var log = new InMemoryActivityLog();
        foreach (var entry in scenario == "logs-empty" ? [] : LogEntries())
        {
            log.Append(entry);
        }

        var paths = new AppPaths(
            @"C:\Users\gustavo\AppData\Roaming\GapMouse",
            @"C:\Users\gustavo\AppData\Local\GapMouse\logs",
            false);

        var home = new HomeViewModel(l, engine, ui, time, new NoopPausePrompt(), new NoopAppLifetime());
        home.Refresh();
        var logs = new LogsViewModel(l, log, ui, new NoopClipboardService(), new NoopShellLauncher(), paths.LogsDirectory);
        var settingsVm = new SettingsViewModel(l, settings);
        if (scenario == "settings-status")
        {
            settingsVm.IdleText = "121";
            settingsVm.SaveCommand.Execute(null);
        }

        var about = new AboutViewModel(l, new NeverUpdateChecker(), new NoopShellLauncher(), ui, paths, "1.1.0");
        var main = new MainWindowViewModel(l, home, logs, settingsVm, about);
        return new Scene(l, main, [home, logs, settingsVm, about]);
    }

    private static EngineSnapshot SnapshotFor(string scenario)
    {
        var baseline = new FakeEngineController().Snapshot with
        {
            StartedAt = At(8, 57, 13),
            LastUserReturnedAt = At(14, 12, 40),
            LastPausedAt = At(12, 1, 5),
            LastResumedAt = At(13, 2, 11),
            Today = new DailyStats(new DateOnly(2026, 11, 1), new TimeSpan(1, 12, 30), 9),
        };

        return scenario switch
        {
            "home-moving" => baseline with
            {
                State = EngineState.Moving,
                LastIdle = TimeSpan.FromSeconds(130),
                IdleThreshold = TimeSpan.FromSeconds(120),
                LastMovingStartedAt = At(14, 30, 2),
            },
            "home-paused" => baseline with
            {
                State = EngineState.Paused,
                LastIdle = TimeSpan.FromSeconds(45),
                IdleThreshold = TimeSpan.FromSeconds(120),
                LastMovingStartedAt = At(14, 30, 2),
                PauseEndsAt = Now + new TimeSpan(0, 9, 41),
            },
            _ => baseline with
            {
                State = EngineState.Waiting,
                LastIdle = TimeSpan.FromSeconds(45),
                IdleThreshold = TimeSpan.FromSeconds(120),
                LastMovingStartedAt = At(14, 30, 2),
            },
        };
    }

    private static DateTimeOffset At(int h, int m, int s) => new(2026, 11, 1, h, m, s, TimeSpan.Zero);

    private static IEnumerable<LogEntry> LogEntries() =>
    [
        new(At(8, 57, 13), LogKind.Start, "GapMouse started (idle threshold 120 s)."),
        new(At(9, 3, 41), LogKind.Config, "Settings changed: idle threshold 120 s, movement 1-10 s."),
        new(At(9, 40, 2), LogKind.Waiting, "Waiting for inactivity (idle 45 s of 120 s)."),
        new(At(10, 15, 27), LogKind.Moving, "Idle for 120 s. Moving the cursor to keep the PC awake."),
        new(At(10, 15, 38), LogKind.Return, "User activity detected. Returning control to the user."),
        new(At(12, 1, 5), LogKind.Paused, "Paused for 1 hour by the user."),
        new(At(12, 1, 6), LogKind.Warn, "Port Cursor failed: Access denied."),
        new(At(13, 2, 11), LogKind.Resumed, "Resumed after the pause."),
        new(At(13, 20, 50), LogKind.Moving, "Idle for 120 s. Moving the cursor to keep the PC awake."),
        new(At(13, 21, 3), LogKind.Return, "User activity detected. Returning control to the user."),
        new(At(13, 58, 19), LogKind.Error, "Power request could not be released because the engine thread was interrupted unexpectedly while the system was changing power state; it will be retried on the next cycle."),
        new(At(14, 12, 40), LogKind.Return, "User activity detected. Returning control to the user."),
        new(At(14, 30, 2), LogKind.Moving, "Idle for 120 s. Moving the cursor to keep the PC awake."),
        new(At(14, 31, 55), LogKind.Stop, "GapMouse stopped."),
    ];
}
