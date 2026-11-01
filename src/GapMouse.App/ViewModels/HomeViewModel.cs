using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GapMouse.App.Localization;
using GapMouse.App.Services;
using GapMouse.Application.Engine;
using GapMouse.Domain.Engine;

namespace GapMouse.App.ViewModels;

public sealed partial class HomeViewModel : ViewModelBase, IDisposable
{
    private readonly IEngineController _engine;
    private readonly IUiDispatcher _ui;
    private readonly TimeProvider _time;
    private readonly IPausePrompt _pausePrompt;
    private readonly IAppLifetime _lifetime;
    private ITimer? _timer;
    private bool _disposed;

    [ObservableProperty]
    private EngineState _state;

    [ObservableProperty]
    private string _stateText = string.Empty;

    [ObservableProperty]
    private string _idleText = string.Empty;

    [ObservableProperty]
    private double _idleProgress;

    [ObservableProperty]
    private string? _pauseText;

    [ObservableProperty]
    private string _startedText = string.Empty;

    [ObservableProperty]
    private string _lastMovementText = string.Empty;

    [ObservableProperty]
    private string _lastReturnText = string.Empty;

    [ObservableProperty]
    private string _lastPauseText = string.Empty;

    [ObservableProperty]
    private string _lastResumeText = string.Empty;

    [ObservableProperty]
    private string _todayActiveTime = string.Empty;

    [ObservableProperty]
    private int _todayActivations;

    public HomeViewModel(
        Localizer l,
        IEngineController engine,
        IUiDispatcher ui,
        TimeProvider time,
        IPausePrompt pausePrompt,
        IAppLifetime lifetime)
        : base(l)
    {
        _engine = engine;
        _ui = ui;
        _time = time;
        _pausePrompt = pausePrompt;
        _lifetime = lifetime;
        L.PropertyChanged += OnLocalizerChanged;
    }

    public bool IsWaiting => State == EngineState.Waiting;

    public bool IsMoving => State == EngineState.Moving;

    public bool IsPaused => State == EngineState.Paused;

    public bool HasPauseText => PauseText is not null;

    public void Start()
    {
        if (_disposed || _timer is not null)
        {
            return;
        }

        _engine.EventRaised += OnEngineEvent;
        _timer = _time.CreateTimer(_ => PostRefresh(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        Refresh();
    }

    public void Refresh()
    {
        if (_disposed)
        {
            return;
        }

        var s = _engine.Snapshot;
        var now = _time.GetLocalNow();
        State = s.State;
        StateText = s.State switch
        {
            EngineState.Moving => L.HomeStateMoving,
            EngineState.Paused => L.HomeStatePaused,
            _ => L.HomeStateWaiting,
        };

        var threshold = s.IdleThreshold;
        var idle = s.LastIdle < threshold ? s.LastIdle : threshold;
        if (idle < TimeSpan.Zero)
        {
            idle = TimeSpan.Zero;
        }

        IdleText = L.FormatIdle((int)idle.TotalSeconds, (int)threshold.TotalSeconds);
        IdleProgress = threshold > TimeSpan.Zero ? idle / threshold : 0;

        if (s.State == EngineState.Paused)
        {
            if (s.PauseEndsAt is { } ends)
            {
                var left = ends - now;
                PauseText = L.FormatResumesIn(FormatDuration(left < TimeSpan.Zero ? TimeSpan.Zero : left));
            }
            else
            {
                PauseText = L.HomePausedUntilResume;
            }
        }
        else
        {
            PauseText = null;
        }

        StartedText = FormatTime(s.StartedAt);
        LastMovementText = FormatTime(s.LastMovingStartedAt);
        LastReturnText = FormatTime(s.LastUserReturnedAt);
        LastPauseText = FormatTime(s.LastPausedAt);
        LastResumeText = FormatTime(s.LastResumedAt);
        TodayActiveTime = FormatDuration(s.Today.MovingTime);
        TodayActivations = s.Today.MovingActivations;

        OnPropertyChanged(nameof(IsWaiting));
        OnPropertyChanged(nameof(IsMoving));
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(HasPauseText));
        ResumeCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer?.Dispose();
        _engine.EventRaised -= OnEngineEvent;
        L.PropertyChanged -= OnLocalizerChanged;
    }

    [RelayCommand]
    private async Task PauseAsync(PauseOption option)
    {
        int? custom = null;
        if (option == PauseOption.Custom)
        {
            custom = await _pausePrompt.AskMinutesAsync();
            if (custom is null)
            {
                return;
            }
        }

        _engine.Pause(PauseOptions.ToDuration(option, custom));
    }

    [RelayCommand(CanExecute = nameof(IsPaused))]
    private void Resume() => _engine.Resume();

    [RelayCommand]
    private void Exit() => _lifetime.Exit();

    private static string FormatDuration(TimeSpan value)
    {
        var total = (long)value.TotalSeconds;
        var hours = total / 3600;
        var minutes = total / 60 % 60;
        var seconds = total % 60;
        return hours > 0
            ? $"{hours}:{minutes:00}:{seconds:00}"
            : $"{minutes:00}:{seconds:00}";
    }

    private string FormatTime(DateTimeOffset? value) =>
        value is { } v
            ? TimeZoneInfo.ConvertTime(v, _time.LocalTimeZone).ToString("HH:mm:ss", L.Culture)
            : L.None;

    private void PostRefresh() => _ui.Post(Refresh);

    private void OnEngineEvent(object? sender, EngineEventArgs e) => PostRefresh();

    private void OnLocalizerChanged(object? sender, PropertyChangedEventArgs e) => PostRefresh();
}
