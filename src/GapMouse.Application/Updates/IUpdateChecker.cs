namespace GapMouse.Application.Updates;

/// <summary>Checks for a newer release.</summary>
public interface IUpdateChecker
{
    Task<UpdateCheckResult> CheckNowAsync(CancellationToken cancellationToken);

    event EventHandler<UpdateCheckResult>? UpdateFound;
}
