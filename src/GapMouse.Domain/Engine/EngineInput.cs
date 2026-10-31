using GapMouse.Domain.Geometry;

namespace GapMouse.Domain.Engine;

/// <summary>A sample of the outside world handed to the engine; <paramref name="Now"/> is local time.</summary>
public readonly record struct EngineInput(DateTimeOffset Now, TimeSpan Idle, PixelPoint Cursor, ScreenBounds Monitor);
