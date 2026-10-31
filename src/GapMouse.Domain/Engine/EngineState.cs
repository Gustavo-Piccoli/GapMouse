namespace GapMouse.Domain.Engine;

/// <summary>The high-level state of the keep-awake engine.</summary>
public enum EngineState
{
    Waiting,
    Moving,
    Paused,
}
