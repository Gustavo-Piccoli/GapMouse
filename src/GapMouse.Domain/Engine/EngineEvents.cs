using GapMouse.Domain.Pausing;
using GapMouse.Domain.Settings;

namespace GapMouse.Domain.Engine;

/// <summary>A notable thing that happened inside the engine.</summary>
public abstract record EngineEvent(DateTimeOffset At);

public sealed record EngineStarted(DateTimeOffset At) : EngineEvent(At);

public sealed record WaitingStarted(DateTimeOffset At, TimeSpan IdleThreshold) : EngineEvent(At);

public sealed record MovingStarted(DateTimeOffset At, TimeSpan IntervalMin, TimeSpan IntervalMax) : EngineEvent(At);

public sealed record UserReturned(DateTimeOffset At) : EngineEvent(At);

public sealed record EnginePaused(DateTimeOffset At, PauseDuration Duration, DateTimeOffset? ResumesAt) : EngineEvent(At);

public sealed record EngineResumed(DateTimeOffset At, ResumeReason Reason) : EngineEvent(At);

public sealed record SessionLocked(DateTimeOffset At, bool KeepAwake) : EngineEvent(At);

public sealed record SessionUnlocked(DateTimeOffset At) : EngineEvent(At);

public sealed record SettingsChanged(DateTimeOffset At, EngineSettings Settings) : EngineEvent(At);

public sealed record EngineStopped(DateTimeOffset At) : EngineEvent(At);
