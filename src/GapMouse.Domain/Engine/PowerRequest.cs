namespace GapMouse.Domain.Engine;

/// <summary>What the host should ask the OS to keep awake.</summary>
public enum PowerRequest
{
    None,
    KeepSystemAwake,
    KeepSystemAndDisplayAwake,
}
