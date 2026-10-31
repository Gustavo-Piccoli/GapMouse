using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using GapMouse.Application.Abstractions;
using GapMouse.Domain.Engine;
using GapMouse.Domain.Geometry;
using GapMouse.Domain.Pausing;
using GapMouse.Domain.Randomness;
using GapMouse.Domain.Settings;
using Microsoft.Extensions.Logging;

namespace GapMouse.Application.Engine;

/// <summary>
/// Imperative shell around <see cref="KeepAwakeEngine"/>: runs it on one dedicated thread and talks to the OS through ports.
/// Every port call, including every power request, happens on that thread because Win32 execution state is per-thread.
/// </summary>
public sealed partial class EngineHost : IEngineController, IDisposable
{
    private static readonly TimeSpan MinimumWait = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan MaximumWait = TimeSpan.FromMilliseconds(int.MaxValue);
    private static readonly TimeSpan JoinTimeout = TimeSpan.FromSeconds(5);

    private readonly EngineSettings _initialSettings;
    private readonly IIdleTimeProvider _idle;
    private readonly ICursorController _cursor;
    private readonly IMonitorBoundsProvider _monitors;
    private readonly IPowerRequestService _power;
    private readonly IRandomSource _random;
    private readonly TimeProvider _time;
    private readonly ILogger<EngineHost> _logger;
    private readonly DailyStats? _restoredStats;

    private readonly ConcurrentQueue<Action<KeepAwakeEngine, DateTimeOffset>> _commands = new();
    private readonly AutoResetEvent _wake = new(false);
    private readonly bool[] _failing = new bool[Enum.GetValues<Port>().Length];
    private readonly Thread _thread;

    private EngineSnapshot _snapshot;
    private int _started;
    private int _disposed;
    private volatile bool _stopRequested;

    public EngineHost(
        EngineSettings initialSettings,
        IIdleTimeProvider idle,
        ICursorController cursor,
        IMonitorBoundsProvider monitors,
        IPowerRequestService power,
        IRandomSource random,
        TimeProvider time,
        ILogger<EngineHost> logger,
        DailyStats? restoredStats = null)
    {
        ArgumentNullException.ThrowIfNull(initialSettings);
        ArgumentNullException.ThrowIfNull(idle);
        ArgumentNullException.ThrowIfNull(cursor);
        ArgumentNullException.ThrowIfNull(monitors);
        ArgumentNullException.ThrowIfNull(power);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);

        _initialSettings = initialSettings;
        _idle = idle;
        _cursor = cursor;
        _monitors = monitors;
        _power = power;
        _random = random;
        _time = time;
        _logger = logger;
        _restoredStats = restoredStats;
        _snapshot = new KeepAwakeEngine(initialSettings, random, time.GetLocalNow(), restoredStats).GetSnapshot();
        _thread = new Thread(Run) { IsBackground = true, Name = "GapMouse.Engine" };
    }

    private enum Port
    {
        Idle,
        Cursor,
        Monitor,
        SetCursor,
        Power,
        Loop,
    }

    public event EventHandler<EngineEventArgs>? EventRaised;

    public EngineSnapshot Snapshot => Volatile.Read(ref _snapshot);

    /// <summary>Starts the engine thread; may be called only once.</summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            throw new InvalidOperationException("The engine host has already been started.");
        }

        _thread.Start();
    }

    public void Pause(PauseDuration duration)
    {
        ArgumentNullException.ThrowIfNull(duration);
        Enqueue((engine, now) => engine.Pause(duration, now));
    }

    public void Resume() => Enqueue((engine, now) => engine.Resume(now));

    public void ApplySettings(EngineSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Enqueue((engine, now) => engine.ApplySettings(settings, now));
    }

    /// <summary>Signals the engine thread to stop, waits up to five seconds for it and releases the power request.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _stopRequested = true;
        _wake.Set();
        if (Volatile.Read(ref _started) == 0)
        {
            _wake.Dispose();
        }
        else if (Environment.CurrentManagedThreadId != _thread.ManagedThreadId && _thread.Join(JoinTimeout))
        {
            _wake.Dispose();
        }
    }

    /// <summary>Clamps a requested wait so the loop can neither spin nor block forever.</summary>
    internal static TimeSpan ClampWait(TimeSpan wait) =>
        wait < MinimumWait ? MinimumWait : wait > MaximumWait ? MaximumWait : wait;

    internal void Enqueue(Action<KeepAwakeEngine, DateTimeOffset> command)
    {
        if (_stopRequested)
        {
            return;
        }

        _commands.Enqueue(command);
        try
        {
            _wake.Set();
        }
        catch (ObjectDisposedException)
        {
            // Raced with Dispose: the host is gone, so the command is moot.
        }
    }

    private void Run()
    {
        KeepAwakeEngine? engine = null;
        try
        {
            engine = new KeepAwakeEngine(_initialSettings, _random, _time.GetLocalNow(), _restoredStats);
            PowerRequest? applied = null;
            while (!_stopRequested)
            {
                var wait = SafeIterate(engine, ref applied);
                if (_stopRequested)
                {
                    break;
                }

                _wake.WaitOne(ClampWait(wait));
            }
        }
        finally
        {
            Shutdown(engine);
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "An unexpected failure in one iteration must not terminate the process.")]
    private TimeSpan SafeIterate(KeepAwakeEngine engine, ref PowerRequest? applied)
    {
        try
        {
            var wait = Iterate(engine, ref applied);
            _failing[(int)Port.Loop] = false;
            return wait;
        }
        catch (Exception ex)
        {
            if (!_failing[(int)Port.Loop])
            {
                _failing[(int)Port.Loop] = true;
                LogLoopFailed(ex);
            }

            return engine.Settings.CheckInterval;
        }
    }

    private TimeSpan Iterate(KeepAwakeEngine engine, ref PowerRequest? applied)
    {
        while (_commands.TryDequeue(out var command))
        {
            command(engine, _time.GetLocalNow());
        }

        var wait = engine.Settings.CheckInterval;
        if (TryReadInput(out var input))
        {
            var step = engine.Advance(input);
            wait = step.WaitBeforeNext;

            ApplyPower(step.Power, ref applied);

            if (step.MoveCursorTo is { } target)
            {
                MoveCursor(target);
            }
        }
        else if (engine.State == EngineState.Paused || engine.IsStopped)
        {
            // Without readable input there is no step, but a pause must still release the power request.
            ApplyPower(PowerRequest.None, ref applied);
        }

        RaiseEvents(engine);
        Volatile.Write(ref _snapshot, engine.GetSnapshot());
        return wait;
    }

    private void ApplyPower(PowerRequest request, ref PowerRequest? applied)
    {
        if (applied != request && TryPort(Port.Power, () => _power.Apply(request)))
        {
            applied = request;
        }
    }

    private bool TryReadInput(out EngineInput input)
    {
        input = default;
        var now = _time.GetLocalNow();
        TimeSpan idle = default;
        PixelPoint cursor = default;
        ScreenBounds monitor = default;
        var ok = TryPort(Port.Idle, () => idle = _idle.GetIdleTime())
            && TryPort(Port.Cursor, () => cursor = _cursor.GetPosition())
            && TryPort(Port.Monitor, () => monitor = _monitors.GetBoundsAt(cursor));
        if (ok)
        {
            input = new EngineInput(now, idle, cursor, monitor);
        }

        return ok;
    }

    private void MoveCursor(PixelPoint target)
    {
        var moved = false;
        if (!TryPort(Port.SetCursor, () => moved = _cursor.TrySetPosition(target)))
        {
            return;
        }

        if (moved)
        {
            ReportRecovery(Port.SetCursor);
        }
        else
        {
            ReportFailure(Port.SetCursor, "the system refused the move", null);
        }
    }

    /// <summary>Runs a port call; logs the first failure of a streak and one line when the port recovers.</summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Ports are external; any failure must be contained so the engine thread never dies.")]
    private bool TryPort(Port port, Action call)
    {
        try
        {
            call();
        }
        catch (Exception ex)
        {
            ReportFailure(port, ex.Message, ex);
            return false;
        }

        // A refused move is reported by the caller after this returns, so recovery is decided there too.
        if (port != Port.SetCursor)
        {
            ReportRecovery(port);
        }

        return true;
    }

    private void ReportFailure(Port port, string reason, Exception? exception)
    {
        if (_failing[(int)port])
        {
            return;
        }

        _failing[(int)port] = true;
        LogPortFailed(exception, port, reason);
    }

    private void ReportRecovery(Port port)
    {
        if (!_failing[(int)port])
        {
            return;
        }

        _failing[(int)port] = false;
        LogPortRecovered(port);
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A faulty subscriber must not kill the engine thread.")]
    private void RaiseEvents(KeepAwakeEngine engine)
    {
        foreach (var engineEvent in engine.DrainEvents())
        {
            try
            {
                EventRaised?.Invoke(this, new EngineEventArgs(engineEvent));
            }
            catch (Exception ex)
            {
                LogSubscriberFailed(ex);
            }
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Shutdown must always reach the final power release.")]
    private void Shutdown(KeepAwakeEngine? engine)
    {
        try
        {
            if (engine is not null)
            {
                engine.Stop(_time.GetLocalNow());
                RaiseEvents(engine);
                Volatile.Write(ref _snapshot, engine.GetSnapshot());
            }
        }
        catch (Exception ex)
        {
            LogSubscriberFailed(ex);
        }
        finally
        {
            TryPort(Port.Power, () => _power.Apply(PowerRequest.None));
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Port {Port} failed: {Reason}")]
    private partial void LogPortFailed(Exception? exception, Port port, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Port {Port} recovered")]
    private partial void LogPortRecovered(Port port);

    [LoggerMessage(Level = LogLevel.Error, Message = "An engine loop iteration failed; continuing")]
    private partial void LogLoopFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "An engine event subscriber or shutdown step threw")]
    private partial void LogSubscriberFailed(Exception exception);
}
