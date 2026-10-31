namespace GapMouse.Application.Logging;

/// <summary>The category of an activity log entry.</summary>
public enum LogKind
{
    Start,
    Waiting,
    Moving,
    Return,
    Paused,
    Resumed,
    Config,
    Stop,
    Warn,
    Error,
}
