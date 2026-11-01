using GapMouse.Application.Engine;
using GapMouse.Domain.Engine;
using GapMouse.Domain.Pausing;
using GapMouse.Domain.Settings;

namespace GapMouse.Testing.Engine;

public sealed class FakeEngineController : IEngineController
{
    public EngineSnapshot Snapshot { get; set; } = new(
        EngineState.Waiting,
        DateTimeOffset.UnixEpoch,
        TimeSpan.Zero,
        EngineSettings.Default.IdleThreshold,
        null,
        null,
        null,
        null,
        null,
        DailyStats.Empty(new DateOnly(2026, 1, 1)),
        false);

    public List<PauseDuration> PauseCalls { get; } = [];

    public int ResumeCalls { get; private set; }

    public List<EngineSettings> ApplySettingsCalls { get; } = [];

    public event EventHandler<EngineEventArgs>? EventRaised;

    public void Raise(EngineEvent e) => EventRaised?.Invoke(this, new EngineEventArgs(e));

    public void Pause(PauseDuration duration) => PauseCalls.Add(duration);

    public void Resume() => ResumeCalls++;

    public void ApplySettings(EngineSettings settings) => ApplySettingsCalls.Add(settings);
}
