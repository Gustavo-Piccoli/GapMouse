namespace GapMouse.Application.Logging;

/// <summary>Bounded in-memory log (newest <see cref="Capacity"/> entries) feeding the UI log view.</summary>
public sealed class InMemoryActivityLog : IActivityLog
{
    public const int Capacity = 1000;

    private readonly object _gate = new();
    private readonly Queue<LogEntry> _entries = new();

    /// <summary>Raised on the appending thread, outside the internal lock.</summary>
    public event EventHandler<LogEntry>? EntryAdded;

    public void Append(LogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_gate)
        {
            _entries.Enqueue(entry);
            while (_entries.Count > Capacity)
            {
                _entries.Dequeue();
            }
        }

        EntryAdded?.Invoke(this, entry);
    }

    /// <summary>A snapshot copy, oldest first.</summary>
    public IReadOnlyList<LogEntry> GetEntries()
    {
        lock (_gate)
        {
            return [.. _entries];
        }
    }
}
