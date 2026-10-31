using System.Text.Json;
using GapMouse.Application.Tests.Fakes;
using GapMouse.Application.Updates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace GapMouse.Application.Tests.Updates;

public class UpdateServiceTests
{
    private static readonly Version Current = new(1, 0, 0);
    private static readonly Uri PageUrl = new("https://github.com/Gustavo-Piccoli/GapMouse/releases/tag/v1.1.0");

    private sealed class FakeSource(Func<CancellationToken, Task<ReleaseInfo?>> handler) : IReleaseSource
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public Task<ReleaseInfo?> GetLatestAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            return handler(cancellationToken);
        }
    }

    private static FakeSource Returning(string? tag) =>
        new(_ => Task.FromResult<ReleaseInfo?>(tag is null ? null : new ReleaseInfo(tag, PageUrl)));

    private static FakeSource Throwing(Exception ex) => new(_ => Task.FromException<ReleaseInfo?>(ex));

    private static FakeSource Hanging() =>
        new(async ct =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return null;
        });

    private static UpdateService Create(IReleaseSource source, FakeTimeProvider time, ListLogger<UpdateService>? logger = null) =>
        new(source, Current, time, logger ?? new ListLogger<UpdateService>());

    [Fact]
    public async Task CheckNow_NewerTag_ReturnsUpdateAvailableWithUrl()
    {
        using var sut = Create(Returning("v1.1.0"), new FakeTimeProvider());

        var result = await sut.CheckNowAsync(TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(UpdateCheckOutcome.UpdateAvailable);
        result.LatestVersion.ShouldBe(new Version(1, 1, 0));
        result.ReleaseUrl.ShouldBe(PageUrl);
    }

    [Fact]
    public async Task CheckNow_SameTag_ReturnsUpToDate()
    {
        using var sut = Create(Returning("v1.0.0"), new FakeTimeProvider());

        (await sut.CheckNowAsync(TestContext.Current.CancellationToken)).Outcome.ShouldBe(UpdateCheckOutcome.UpToDate);
    }

    [Fact]
    public async Task CheckNow_OlderTag_ReturnsUpToDate()
    {
        using var sut = Create(Returning("v0.9.0"), new FakeTimeProvider());

        (await sut.CheckNowAsync(TestContext.Current.CancellationToken)).Outcome.ShouldBe(UpdateCheckOutcome.UpToDate);
    }

    [Fact]
    public async Task CheckNow_NullRelease_ReturnsNoReleases()
    {
        using var sut = Create(Returning(null), new FakeTimeProvider());

        (await sut.CheckNowAsync(TestContext.Current.CancellationToken)).Outcome.ShouldBe(UpdateCheckOutcome.NoReleases);
    }

    [Fact]
    public async Task CheckNow_SourceThrowsHttpRequest_ReturnsFailed()
    {
        var logger = new ListLogger<UpdateService>();
        using var sut = Create(Throwing(new HttpRequestException("boom")), new FakeTimeProvider(), logger);

        (await sut.CheckNowAsync(TestContext.Current.CancellationToken)).Outcome.ShouldBe(UpdateCheckOutcome.Failed);
        logger.Levels.ShouldNotBeEmpty();
        logger.Levels.ShouldAllBe(l => l < LogLevel.Warning);
    }

    [Fact]
    public async Task CheckNow_SourceThrowsJson_ReturnsFailed()
    {
        using var sut = Create(Throwing(new JsonException("bad")), new FakeTimeProvider());

        (await sut.CheckNowAsync(TestContext.Current.CancellationToken)).Outcome.ShouldBe(UpdateCheckOutcome.Failed);
    }

    [Fact]
    public async Task CheckNow_UnparsableTag_ReturnsFailed()
    {
        using var sut = Create(Returning("latest"), new FakeTimeProvider());

        (await sut.CheckNowAsync(TestContext.Current.CancellationToken)).Outcome.ShouldBe(UpdateCheckOutcome.Failed);
    }

    [Fact]
    public async Task CheckNow_SourceHangs_TimesOutAfterTenSeconds()
    {
        var time = new FakeTimeProvider();
        using var sut = Create(Hanging(), time);

        var task = sut.CheckNowAsync(TestContext.Current.CancellationToken);
        task.IsCompleted.ShouldBeFalse();
        time.Advance(TimeSpan.FromSeconds(10));

        (await task).Outcome.ShouldBe(UpdateCheckOutcome.Failed);
    }

    [Fact]
    public async Task CheckNow_CallerCancels_Throws()
    {
        using var sut = Create(Hanging(), new FakeTimeProvider());
        using var cts = new CancellationTokenSource();

        var task = sut.CheckNowAsync(cts.Token);
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(task);
    }

    [Fact]
    public async Task Start_Enabled_ChecksImmediatelyAndEvery24Hours()
    {
        var time = new FakeTimeProvider();
        var source = Returning("v1.0.0");
        using var sut = Create(source, time);

        sut.Start(() => true);
        await WaitForCallsAsync(source, 1);

        time.Advance(TimeSpan.FromHours(24));
        await WaitForCallsAsync(source, 2);
        source.Calls.ShouldBe(2);
    }

    [Fact]
    public async Task Start_Disabled_NeverCallsSource()
    {
        var time = new FakeTimeProvider();
        var source = Returning("v1.1.0");
        using var sut = Create(source, time);

        sut.Start(() => false);
        time.Advance(TimeSpan.FromHours(48));
        await Task.Delay(100, TestContext.Current.CancellationToken);

        source.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Start_DisabledThenEnabled_ChecksOnNextTick()
    {
        var time = new FakeTimeProvider();
        var source = Returning("v1.0.0");
        var enabled = false;
        using var sut = Create(source, time);

        sut.Start(() => Volatile.Read(ref enabled));
        await Task.Delay(100, TestContext.Current.CancellationToken);
        source.Calls.ShouldBe(0);

        Volatile.Write(ref enabled, true);
        time.Advance(TimeSpan.FromHours(24));
        await WaitForCallsAsync(source, 1);
    }

    [Fact]
    public async Task UpdateFound_RaisedOnlyForNewerVersion()
    {
        var time = new FakeTimeProvider();
        var tag = "v1.0.0";
        var source = new FakeSource(_ => Task.FromResult<ReleaseInfo?>(new ReleaseInfo(Volatile.Read(ref tag), PageUrl)));
        var raised = new List<UpdateCheckResult>();
        using var sut = Create(source, time);
        sut.UpdateFound += (_, r) =>
        {
            lock (raised)
            {
                raised.Add(r);
            }
        };

        await sut.CheckNowAsync(TestContext.Current.CancellationToken);
        lock (raised)
        {
            raised.ShouldBeEmpty();
        }

        Volatile.Write(ref tag, "v1.1.0");
        await sut.CheckNowAsync(TestContext.Current.CancellationToken);

        lock (raised)
        {
            raised.Count.ShouldBe(1);
            raised[0].Outcome.ShouldBe(UpdateCheckOutcome.UpdateAvailable);
        }
    }

    private static async Task WaitForCallsAsync(FakeSource source, int expected)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (source.Calls < expected && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        source.Calls.ShouldBeGreaterThanOrEqualTo(expected);
    }
}
