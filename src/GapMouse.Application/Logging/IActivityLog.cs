namespace GapMouse.Application.Logging;

/// <summary>A sink for activity log entries.</summary>
public interface IActivityLog
{
    void Append(LogEntry entry);
}
