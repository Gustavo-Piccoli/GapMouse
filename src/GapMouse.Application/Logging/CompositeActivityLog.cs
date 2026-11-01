using System.Diagnostics.CodeAnalysis;

namespace GapMouse.Application.Logging;

/// <summary>Fans an entry out to several sinks; a failing sink never stops the others.</summary>
public sealed class CompositeActivityLog(IReadOnlyList<IActivityLog> sinks) : IActivityLog
{
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Logging is best-effort; one faulty sink must not prevent the others from receiving the entry.")]
    public void Append(LogEntry entry)
    {
        foreach (var sink in sinks)
        {
            try
            {
                sink.Append(entry);
            }
            catch (Exception)
            {
                // Intentionally ignored: there is nowhere safe to report a logging failure.
            }
        }
    }
}
