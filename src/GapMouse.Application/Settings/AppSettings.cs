using GapMouse.Domain.Settings;

namespace GapMouse.Application.Settings;

/// <summary>All user-configurable settings: the engine settings plus app-level preferences.</summary>
public sealed record AppSettings
{
    public const int MinLogRetentionDays = 1;

    public const int MaxLogRetentionDays = 365;

    private readonly string _pauseHotkey = "Ctrl+Alt+P";

    private readonly int _logRetentionDays = 7;

    public static AppSettings Default { get; } = new();

    public EngineSettings Engine { get; init; } = EngineSettings.Default;

    public bool StartWithWindows { get; init; }

    public bool StartMinimized { get; init; }

    public bool NotificationsEnabled { get; init; }

    /// <summary>Gets the hotkey text as stored; Plan 3 validates its syntax.</summary>
    public string PauseHotkey
    {
        get => _pauseHotkey;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _pauseHotkey = value;
        }
    }

    public bool CheckForUpdates { get; init; } = true;

    public AppTheme Theme { get; init; } = AppTheme.Dark;

    public UiLanguage Language { get; init; } = UiLanguage.Auto;

    public bool SaveLogsToFile { get; init; } = true;

    public int LogRetentionDays
    {
        get => _logRetentionDays;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, MinLogRetentionDays);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaxLogRetentionDays);
            _logRetentionDays = value;
        }
    }

    public bool TrayHintShown { get; init; }
}
