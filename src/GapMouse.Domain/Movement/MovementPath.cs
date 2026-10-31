namespace GapMouse.Domain.Movement;

/// <summary>A planned cursor path plus the pause to take once it is finished.</summary>
public sealed record MovementPath(IReadOnlyList<MovementStep> Steps, TimeSpan SettleDelay);
