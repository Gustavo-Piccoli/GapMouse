using GapMouse.Application.Abstractions;

namespace GapMouse.Application.Tests.Fakes;

internal sealed class FakeIdle : IIdleTimeProvider
{
    private long _idleTicks;

    public TimeSpan Idle
    {
        get => TimeSpan.FromTicks(Interlocked.Read(ref _idleTicks));
        set => Interlocked.Exchange(ref _idleTicks, value.Ticks);
    }

    public TimeSpan GetIdleTime() => Idle;
}
