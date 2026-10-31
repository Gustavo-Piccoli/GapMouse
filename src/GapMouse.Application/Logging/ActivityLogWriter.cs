using GapMouse.Application.Engine;

namespace GapMouse.Application.Logging;

/// <summary>Writes every engine event to the activity log while alive.</summary>
public sealed class ActivityLogWriter : IDisposable
{
    private readonly IEngineController _engine;
    private readonly IActivityLog _log;

    public ActivityLogWriter(IEngineController engine, IActivityLog log)
    {
        _engine = engine;
        _log = log;
        _engine.EventRaised += OnEventRaised;
    }

    public void Dispose() => _engine.EventRaised -= OnEventRaised;

    private void OnEventRaised(object? sender, EngineEventArgs e) =>
        _log.Append(LogEntryFormatter.FromEvent(e.Event));
}
