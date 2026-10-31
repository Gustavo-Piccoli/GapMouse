using GapMouse.Application.Engine;
using GapMouse.Domain.Engine;
using Microsoft.Extensions.Logging;

namespace GapMouse.Application.State;

/// <summary>Saves the daily stats on key engine events, periodically while moving, and on dispose.</summary>
public sealed partial class AppStatePersister : IDisposable
{
    private static readonly TimeSpan SavePeriod = TimeSpan.FromSeconds(60);

    private readonly IEngineController _engine;
    private readonly IAppStateStore _store;
    private readonly TimeProvider _time;
    private readonly ILogger<AppStatePersister> _logger;
    private readonly object _gate = new();
    private ITimer? _timer;
    private bool _started;
    private bool _disposed;
    private bool _failing;

    public AppStatePersister(IEngineController engine, IAppStateStore store, TimeProvider time, ILogger<AppStatePersister> logger)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);
        _engine = engine;
        _store = store;
        _time = time;
        _logger = logger;
    }

    /// <summary>Subscribes to engine events and starts the periodic timer.</summary>
    public void Start()
    {
        lock (_gate)
        {
            if (_started || _disposed)
            {
                return;
            }

            _started = true;
            _engine.EventRaised += OnEngineEvent;
            _timer = _time.CreateTimer(OnTimer, null, SavePeriod, SavePeriod);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _engine.EventRaised -= OnEngineEvent;
            _timer?.Dispose();
            _timer = null;
            SaveLocked();
        }
    }

    private void OnEngineEvent(object? sender, EngineEventArgs e)
    {
        if (e.Event is UserReturned or EnginePaused or EngineStopped)
        {
            Save();
        }
    }

    private void OnTimer(object? state)
    {
        if (_engine.Snapshot.State == EngineState.Moving)
        {
            Save();
        }
    }

    private void Save()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            SaveLocked();
        }
    }

    private void SaveLocked()
    {
        try
        {
            _store.Save(new AppState(_engine.Snapshot.Today));
            _failing = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (!_failing)
            {
                _failing = true;
                LogSaveFailed(ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not save the app state; will keep retrying")]
    private partial void LogSaveFailed(Exception exception);
}
