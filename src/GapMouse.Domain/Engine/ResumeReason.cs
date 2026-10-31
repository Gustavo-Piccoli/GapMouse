namespace GapMouse.Domain.Engine;

/// <summary>Why the engine left the paused state.</summary>
public enum ResumeReason
{
    Manual,
    PauseExpired,
}
