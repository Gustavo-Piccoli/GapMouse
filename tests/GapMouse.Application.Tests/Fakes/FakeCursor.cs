using GapMouse.Application.Abstractions;
using GapMouse.Domain.Geometry;

namespace GapMouse.Application.Tests.Fakes;

internal sealed class FakeCursor : ICursorController
{
    private int _setCount;

    public bool ThrowOnSet { get; set; }

    public int SetCount => Volatile.Read(ref _setCount);

    public PixelPoint GetPosition() => new(960, 540);

    public bool TrySetPosition(PixelPoint position)
    {
        Interlocked.Increment(ref _setCount);
        if (ThrowOnSet)
        {
            throw new InvalidOperationException("cursor boom");
        }

        return true;
    }
}
