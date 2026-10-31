using GapMouse.Domain.Engine;

namespace GapMouse.Application.Abstractions;

/// <summary>Asks the OS to keep the system and/or display awake.</summary>
/// <remarks>Must be called from a single thread; Win32 execution state is per-thread.</remarks>
public interface IPowerRequestService
{
    /// <summary>Applies the requested power state, replacing any previous request.</summary>
    void Apply(PowerRequest request);
}
