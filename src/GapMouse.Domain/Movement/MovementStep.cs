using GapMouse.Domain.Geometry;

namespace GapMouse.Domain.Movement;

/// <summary>One cursor position of a planned path and how long to wait after placing it.</summary>
public readonly record struct MovementStep(PixelPoint Position, TimeSpan DelayAfter);
