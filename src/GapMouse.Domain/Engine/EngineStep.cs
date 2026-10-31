using GapMouse.Domain.Geometry;

namespace GapMouse.Domain.Engine;

/// <summary>What the host must do next: optionally move the cursor, hold a power request, then wait.</summary>
public readonly record struct EngineStep(PixelPoint? MoveCursorTo, PowerRequest Power, TimeSpan WaitBeforeNext);
