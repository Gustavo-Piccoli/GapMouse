namespace GapMouse.Domain.Engine;

/// <summary>An immutable view of the engine for display purposes.</summary>
public sealed record EngineSnapshot(
    EngineState State,
    DateTimeOffset StartedAt,
    TimeSpan LastIdle,
    TimeSpan IdleThreshold,
    DateTimeOffset? LastMovingStartedAt,
    DateTimeOffset? LastUserReturnedAt,
    DateTimeOffset? LastPausedAt,
    DateTimeOffset? LastResumedAt,
    DateTimeOffset? PauseEndsAt,
    DailyStats Today);
