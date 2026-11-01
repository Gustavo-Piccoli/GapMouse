using GapMouse.App.Services;
using GapMouse.Testing.Logging;
using Microsoft.Extensions.Logging;

namespace GapMouse.App.Tests.Services;

public sealed class AppShutdownTests
{
    [Fact]
    public void Exit_RunsStepsInOrder()
    {
        var calls = new List<int>();
        var shutdown = new AppShutdown([() => calls.Add(1), () => calls.Add(2), () => calls.Add(3)], new ListLogger<AppShutdown>());

        shutdown.Exit();

        calls.ShouldBe([1, 2, 3]);
    }

    [Fact]
    public void Exit_StepThrows_RunsRemainingAndLogs()
    {
        var calls = new List<int>();
        var logger = new ListLogger<AppShutdown>();
        var shutdown = new AppShutdown(
            [() => calls.Add(1), () => throw new InvalidOperationException("boom"), () => calls.Add(3)],
            logger);

        shutdown.Exit();

        calls.ShouldBe([1, 3]);
        logger.Levels.ShouldContain(LogLevel.Error);
    }

    [Fact]
    public void Exit_Twice_RunsOnce()
    {
        var count = 0;
        var shutdown = new AppShutdown([() => count++], new ListLogger<AppShutdown>());

        shutdown.Exit();
        shutdown.Exit();

        count.ShouldBe(1);
    }

    [Fact]
    public async Task Exit_ConcurrentCalls_RunsOnce()
    {
        var count = 0;
        var shutdown = new AppShutdown([() => { Interlocked.Increment(ref count); Thread.Sleep(20); }], new ListLogger<AppShutdown>());

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(shutdown.Exit)));

        count.ShouldBe(1);
    }
}
