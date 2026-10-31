namespace GapMouse.Infrastructure.State;

internal sealed class StateDocument
{
    public int? SchemaVersion { get; set; }

    public TodaySection? Today { get; set; }

    internal sealed class TodaySection
    {
        public string? Day { get; set; }

        public double? MovingSeconds { get; set; }

        public int? MovingActivations { get; set; }
    }
}
