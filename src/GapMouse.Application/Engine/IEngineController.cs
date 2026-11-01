using System.Diagnostics.CodeAnalysis;
using GapMouse.Domain.Engine;
using GapMouse.Domain.Pausing;
using GapMouse.Domain.Settings;

namespace GapMouse.Application.Engine;

/// <summary>The surface the UI uses to observe and steer the running engine.</summary>
public interface IEngineController
{
    /// <summary>Gets the latest published snapshot; safe to read from any thread.</summary>
    EngineSnapshot Snapshot { get; }

    /// <summary>Raised on the engine thread for every engine event.</summary>
    event EventHandler<EngineEventArgs>? EventRaised;

    /// <summary>Pauses the engine; takes effect within one loop iteration.</summary>
    void Pause(PauseDuration duration);

    /// <summary>Resumes a paused engine; takes effect within one loop iteration.</summary>
    [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Resume is the domain term; consumers are C# only.")]
    void Resume();

    /// <summary>Replaces the engine settings; takes effect within one loop iteration.</summary>
    void ApplySettings(EngineSettings settings);

    /// <summary>Tells the engine whether the session is locked; takes effect within one loop iteration.</summary>
    void SetSessionLocked(bool locked);
}
