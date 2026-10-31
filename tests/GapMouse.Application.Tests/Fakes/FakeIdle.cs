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

    public bool ThrowOnRead { get; set; }

    public TimeSpan GetIdleTime() => ThrowOnRead ? throw new InvalidOperationException("idle boom") : Idle;
}
