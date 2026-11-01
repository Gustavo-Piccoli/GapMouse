using GapMouse.Application.Abstractions;
using GapMouse.Domain.Geometry;

namespace GapMouse.Application.Tests.Fakes;

internal sealed class FakeCursor : ICursorController
{
    private int _setCount;
    private int _getCount;
    private int _throwOnGetOnce;

    private volatile bool _refuseSet;

    public bool ThrowOnSet { get; set; }

    /// <summary>When true, <see cref="TrySetPosition"/> returns false, like SetCursorPos on a locked desktop.</summary>
    public bool RefuseSet
    {
        get => _refuseSet;
        set => _refuseSet = value;
    }

    public int SetCount => Volatile.Read(ref _setCount);

    public int GetCount => Volatile.Read(ref _getCount);

    /// <summary>Makes the next <see cref="GetPosition"/> call throw once.</summary>
    public void ThrowOnNextGet() => Volatile.Write(ref _throwOnGetOnce, 1);

    public PixelPoint GetPosition()
    {
        Interlocked.Increment(ref _getCount);
        if (Interlocked.Exchange(ref _throwOnGetOnce, 0) == 1)
        {
            throw new InvalidOperationException("Acesso negado.");
        }

        return new(960, 540);
    }

    public bool TrySetPosition(PixelPoint position)
    {
        Interlocked.Increment(ref _setCount);
        if (ThrowOnSet)
        {
            throw new InvalidOperationException("cursor boom");
        }

        return !_refuseSet;
    }
}
