using GapMouse.Domain.Engine;

namespace GapMouse.Application.Engine;

/// <summary>Carries one engine event to subscribers of <see cref="IEngineController.EventRaised"/>.</summary>
public sealed class EngineEventArgs(EngineEvent engineEvent) : EventArgs
{
    public EngineEvent Event { get; } = engineEvent;
}
