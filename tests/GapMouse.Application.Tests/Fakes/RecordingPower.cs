using GapMouse.Application.Abstractions;
using GapMouse.Domain.Engine;

namespace GapMouse.Application.Tests.Fakes;

internal sealed class RecordingPower : IPowerRequestService
{
    private readonly object _gate = new();
    private readonly List<(PowerRequest Request, int ThreadId)> _calls = [];

    public IReadOnlyList<(PowerRequest Request, int ThreadId)> Calls
    {
        get
        {
            lock (_gate)
            {
                return [.. _calls];
            }
        }
    }

    public PowerRequest? Last
    {
        get
        {
            lock (_gate)
            {
                return _calls.Count == 0 ? null : _calls[^1].Request;
            }
        }
    }

    public void Apply(PowerRequest request)
    {
        lock (_gate)
        {
            _calls.Add((request, Environment.CurrentManagedThreadId));
        }
    }
}
