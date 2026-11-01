using System.ComponentModel;
using GapMouse.App.Localization;
using GapMouse.App.Services;
using GapMouse.Application.Engine;
using GapMouse.Application.Settings;
using GapMouse.Application.Shell;
using GapMouse.Application.Updates;
using GapMouse.Domain.Engine;
using GapMouse.Domain.Pausing;

namespace GapMouse.App.ViewModels;

/// <summary>Keeps the tray icon, its menu, balloons and the pause hotkey in step with the engine.</summary>
public sealed class TrayCoordinator(
    Localizer l,
    IEngineController engine,
    ITrayIcon tray,
    IGlobalHotkey hotkey,
    ISettingsService settings,
    IUpdateChecker updates,
    IPausePrompt pausePrompt,
    IMainWindowService window,
    IAppLifetime lifetime,
    IShellLauncher launcher,
    IUiDispatcher ui) : IDisposable
{
    private const string UpdateTagPrefix = "update:";

    private readonly HashSet<Version> _announcedVersions = [];
    private EngineState _state;
    private bool _promptOpen;
    private bool _started;
    private bool _disposed;

    public void Start()
    {
        // Subscribe before reading the snapshot so no event is lost; handlers run on the UI thread
        // via ui.Post, so any event raised meanwhile is applied after this seed and is never older.
        engine.EventRaised += OnEngineEvent;
        _state = engine.Snapshot.State;
        tray.MenuRequested += OnMenuRequested;
        tray.OpenRequested += OnOpenRequested;
        tray.BalloonClicked += OnBalloonClicked;
        hotkey.Pressed += OnHotkeyPressed;
        updates.UpdateFound += OnUpdateFound;
        l.PropertyChanged += OnLocalizerChanged;
        _started = true;
        RefreshTray();
    }

    public void OnMainWindowHidden()
    {
        if (settings.Current.TrayHintShown)
        {
            return;
        }

        // Only a balloon that was actually shown consumes the one-time hint.
        if (tray.ShowBalloon(l.AppName, l.TrayHint, null, silent: true))
        {
            settings.MarkTrayHintShown();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (!_started)
        {
            return;
        }

        engine.EventRaised -= OnEngineEvent;
        tray.MenuRequested -= OnMenuRequested;
        tray.OpenRequested -= OnOpenRequested;
        tray.BalloonClicked -= OnBalloonClicked;
        hotkey.Pressed -= OnHotkeyPressed;
        updates.UpdateFound -= OnUpdateFound;
        l.PropertyChanged -= OnLocalizerChanged;
    }

    private static TrayIconState Map(EngineState state) => state switch
    {
        EngineState.Moving => TrayIconState.Moving,
        EngineState.Paused => TrayIconState.Paused,
        _ => TrayIconState.Waiting,
    };

    private void RefreshTray()
    {
        var name = _state switch
        {
            EngineState.Moving => l.TrayStateMoving,
            EngineState.Paused => l.TrayStatePaused,
            _ => l.TrayStateWaiting,
        };
        tray.SetState(Map(_state), l.TrayTooltip(name));
    }

    private void OnLocalizerChanged(object? sender, PropertyChangedEventArgs e) => RefreshTray();

    private void OnEngineEvent(object? sender, EngineEventArgs e) => ui.Post(() => Handle(e.Event));

    private void Handle(EngineEvent e)
    {
        if (_disposed)
        {
            return;
        }

        string? phrase = null;
        switch (e)
        {
            case MovingStarted:
                _state = EngineState.Moving;
                phrase = l.TrayKeepingAwake;
                break;
            case UserReturned:
                _state = EngineState.Waiting;
                phrase = l.TrayWelcomeBack;
                break;
            case EnginePaused:
                _state = EngineState.Paused;
                phrase = l.TrayStatePaused;
                break;
            case EngineResumed:
                _state = EngineState.Waiting;
                phrase = l.TrayResumed;
                break;
            case SessionLocked:
                // The engine stops moving on lock; only an in-flight Moving state changes (Paused stays Paused).
                if (_state == EngineState.Moving)
                {
                    _state = EngineState.Waiting;
                }

                break;
            case WaitingStarted:
                _state = EngineState.Waiting;
                break;
            default:
                return;
        }

        RefreshTray();
        if (phrase is not null && settings.Current.NotificationsEnabled)
        {
            tray.ShowBalloon(l.AppName, phrase, null, silent: true);
        }
    }

    private void OnMenuRequested(object? sender, EventArgs e)
    {
        var first = _state == EngineState.Paused
            ? new TrayMenuItem("resume", l.TrayMenuResume)
            : new TrayMenuItem(
                "pause",
                l.TrayMenuPause,
                [
                    new TrayMenuItem($"pause:{PauseOption.TenMinutes}", l.TrayPause10),
                    new TrayMenuItem($"pause:{PauseOption.ThirtyMinutes}", l.TrayPause30),
                    new TrayMenuItem($"pause:{PauseOption.OneHour}", l.TrayPause1h),
                    new TrayMenuItem($"pause:{PauseOption.Custom}", l.TrayPauseCustom),
                    new TrayMenuItem($"pause:{PauseOption.Never}", l.TrayPauseNever),
                ]);

        var chosen = tray.ShowMenu(
        [
            first,
            TrayMenuItem.Separator,
            new TrayMenuItem("open", l.TrayMenuOpen),
            new TrayMenuItem("exit", l.TrayMenuExit),
        ]);

        Execute(chosen);
    }

    private void Execute(string? id)
    {
        switch (id)
        {
            case null:
                return;
            case "resume":
                engine.Resume();
                return;
            case "open":
                window.Show();
                return;
            case "exit":
                lifetime.Exit();
                return;
        }

        const string prefix = "pause:";
        if (!id.StartsWith(prefix, StringComparison.Ordinal)
            || !Enum.TryParse<PauseOption>(id[prefix.Length..], out var option))
        {
            return;
        }

        if (option == PauseOption.Custom)
        {
            _ = PauseCustomAsync();
            return;
        }

        engine.Pause(PauseOptions.ToDuration(option, null));
    }

    private async Task PauseCustomAsync()
    {
        if (_promptOpen)
        {
            return;
        }

        _promptOpen = true;
        try
        {
            var minutes = await pausePrompt.AskMinutesAsync();
            if (!_disposed && minutes is { } m)
            {
                engine.Pause(PauseOptions.ToDuration(PauseOption.Custom, m));
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The coordinator has no logger; a failed prompt simply leaves the engine unpaused.
        }
        finally
        {
            _promptOpen = false;
        }
    }

    private void OnOpenRequested(object? sender, EventArgs e) => window.Show();

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        if (_state == EngineState.Paused)
        {
            engine.Resume();
        }
        else
        {
            engine.Pause(PauseDuration.Indefinite);
        }
    }

    private void OnUpdateFound(object? sender, UpdateCheckResult result) => ui.Post(() =>
    {
        if (_disposed || result.LatestVersion is not { } version || result.ReleaseUrl is not { } url
            || _announcedVersions.Contains(version))
        {
            return;
        }

        // Remember the version only once the balloon was shown, so a tray that is not ready yet does not swallow it.
        if (tray.ShowBalloon(
            l.TrayUpdateTitle,
            l.TrayUpdateBody(version.ToString()),
            UpdateTagPrefix + url.AbsoluteUri,
            silent: false))
        {
            _announcedVersions.Add(version);
        }
    });

    private void OnBalloonClicked(object? sender, string? tag)
    {
        if (tag is null
            || !tag.StartsWith(UpdateTagPrefix, StringComparison.Ordinal)
            || !Uri.TryCreate(tag[UpdateTagPrefix.Length..], UriKind.Absolute, out var url))
        {
            return;
        }

        try
        {
            launcher.OpenUrl(url);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            // No handler for the URL; nothing useful to show from a balloon click.
        }
    }
}
