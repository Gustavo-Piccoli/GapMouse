namespace GapMouse.Infrastructure.Settings;

internal sealed class SettingsDocument
{
    public int? SchemaVersion { get; set; }

    public EngineSection? Engine { get; set; }

    public SystemSection? System { get; set; }

    public AppearanceSection? Appearance { get; set; }

    public LogsSection? Logs { get; set; }

    public UiSection? Ui { get; set; }

    internal sealed class EngineSection
    {
        public int? IdleThresholdSeconds { get; set; }

        public int? CheckIntervalSeconds { get; set; }

        public int? MovementIntervalMinSeconds { get; set; }

        public int? MovementIntervalMaxSeconds { get; set; }

        public int? AmplitudePixels { get; set; }

        public bool? KeepDisplayOn { get; set; }
    }

    internal sealed class SystemSection
    {
        public bool? StartWithWindows { get; set; }

        public bool? StartMinimized { get; set; }

        public bool? Notifications { get; set; }

        public string? PauseHotkey { get; set; }

        public bool? CheckForUpdates { get; set; }
    }

    internal sealed class AppearanceSection
    {
        public string? Theme { get; set; }

        public string? Language { get; set; }
    }

    internal sealed class LogsSection
    {
        public bool? SaveToFile { get; set; }

        public int? RetentionDays { get; set; }
    }

    internal sealed class UiSection
    {
        public bool? TrayHintShown { get; set; }
    }
}
