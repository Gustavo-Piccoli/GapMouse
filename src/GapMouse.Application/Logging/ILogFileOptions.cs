namespace GapMouse.Application.Logging;

/// <summary>Runtime options of the on-disk log.</summary>
public interface ILogFileOptions
{
    void Configure(bool enabled, int retentionDays);
}
