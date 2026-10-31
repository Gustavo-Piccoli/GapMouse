using Microsoft.Extensions.Logging;

namespace GapMouse.Application.Updates;

/// <summary>Checks for newer releases on demand and, once started, every 24 hours while enabled.</summary>
public sealed partial class UpdateService(IReleaseSource source, Version currentVersion, TimeProvider time, ILogger<UpdateService> logger) : IDisposable
{
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Period = TimeSpan.FromHours(24);

    private readonly Version _current = ReleaseVersion.Normalize(currentVersion);
    private ITimer? _timer;
    private Func<bool>? _isEnabled;
    private int _running;
    private int _disposed;

    public event EventHandler<UpdateCheckResult>? UpdateFound;

    public async Task<UpdateCheckResult> CheckNowAsync(CancellationToken cancellationToken)
    {
        var result = await CheckCoreAsync(cancellationToken).ConfigureAwait(false);
        if (result.Outcome == UpdateCheckOutcome.UpdateAvailable)
        {
            UpdateFound?.Invoke(this, result);
        }

        return result;
    }

    public void Start(Func<bool> isEnabled)
    {
        ArgumentNullException.ThrowIfNull(isEnabled);
        _isEnabled = isEnabled;
        _timer = time.CreateTimer(OnTick, null, TimeSpan.Zero, Period);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _timer?.Dispose();
        }
    }

    private static UpdateCheckResult Failed() => new(UpdateCheckOutcome.Failed, null, null);

    private async Task<UpdateCheckResult> CheckCoreAsync(CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(CheckTimeout, time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            var release = await source.GetLatestAsync(linked.Token).ConfigureAwait(false);
            if (release is null)
            {
                return new UpdateCheckResult(UpdateCheckOutcome.NoReleases, null, null);
            }

            if (!ReleaseVersion.TryParse(release.TagName, out var latest))
            {
                LogUnparsableTag(release.TagName);
                return Failed();
            }

            var outcome = latest > _current ? UpdateCheckOutcome.UpdateAvailable : UpdateCheckOutcome.UpToDate;
            return new UpdateCheckResult(outcome, latest, release.PageUrl);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogCheckFailed(ex);
            return Failed();
        }
    }

    private void OnTick(object? state)
    {
        if (Volatile.Read(ref _disposed) == 1 || _isEnabled is not { } isEnabled || !isEnabled())
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            return;
        }

        _ = CheckScheduledAsync();
    }

    private async Task CheckScheduledAsync()
    {
        try
        {
            await CheckNowAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogScheduledFailed(ex);
        }
        finally
        {
            Volatile.Write(ref _running, 0);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Update check failed")]
    private partial void LogCheckFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Scheduled update check threw")]
    private partial void LogScheduledFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Update check failed: unparsable release tag {Tag}")]
    private partial void LogUnparsableTag(string tag);
}
