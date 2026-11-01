using GapMouse.App.Localization;
using GapMouse.Application.Logging;
using GapMouse.Domain.Engine;

namespace GapMouse.App.ViewModels;

/// <summary>Localizes the on-screen message of engine-event entries; other entries show their raw message.</summary>
public static class LogMessageLocalizer
{
    public static string Format(LogEntry entry, Localizer l)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(l);
        return entry.Source switch
        {
            EngineStarted => l.LogEngineStarted,
            WaitingStarted w => l.LogWaitingStarted(w.IdleThreshold),
            MovingStarted m => l.LogMovingStarted(m.IntervalMin, m.IntervalMax),
            UserReturned => l.LogUserReturned,
            EnginePaused { ResumesAt: { } until } => l.LogPausedUntil(until),
            EnginePaused => l.LogPausedIndefinitely,
            EngineResumed { Reason: ResumeReason.Manual } => l.LogResumedManual,
            EngineResumed => l.LogResumedExpired,
            SettingsChanged => l.LogSettingsChanged,
            EngineStopped => l.LogEngineStopped,
            _ => entry.Message,
        };
    }
}
