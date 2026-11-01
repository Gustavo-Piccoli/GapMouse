namespace GapMouse.Domain.Settings;

/// <summary>Validated engine configuration. Instances can only be built through <see cref="Create"/>.</summary>
public sealed record EngineSettings
{
    private EngineSettings(
        TimeSpan idleThreshold,
        TimeSpan checkInterval,
        TimeSpan movementIntervalMin,
        TimeSpan movementIntervalMax,
        int amplitudePixels,
        bool keepDisplayOn,
        bool keepAwakeWhileLocked)
    {
        IdleThreshold = idleThreshold;
        CheckInterval = checkInterval;
        MovementIntervalMin = movementIntervalMin;
        MovementIntervalMax = movementIntervalMax;
        AmplitudePixels = amplitudePixels;
        KeepDisplayOn = keepDisplayOn;
        KeepAwakeWhileLocked = keepAwakeWhileLocked;
    }

    public static EngineSettings Default { get; } = new(
        TimeSpan.FromSeconds(120),
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(25),
        120,
        true,
        false);

    public TimeSpan IdleThreshold { get; }

    public TimeSpan CheckInterval { get; }

    public TimeSpan MovementIntervalMin { get; }

    public TimeSpan MovementIntervalMax { get; }

    public int AmplitudePixels { get; }

    public bool KeepDisplayOn { get; }

    public bool KeepAwakeWhileLocked { get; }

    /// <summary>Validates the values and reports every error found, not just the first.</summary>
    public static ValidationResult<EngineSettings> Create(
        int idleThresholdSeconds,
        int checkIntervalSeconds,
        int movementIntervalMinSeconds,
        int movementIntervalMaxSeconds,
        int amplitudePixels,
        bool keepDisplayOn,
        bool keepAwakeWhileLocked = false)
    {
        List<SettingsError> errors = [];
        CheckRange(errors, SettingsField.IdleThreshold, idleThresholdSeconds, 10, 3600);
        CheckRange(errors, SettingsField.CheckInterval, checkIntervalSeconds, 1, 60);
        CheckRange(errors, SettingsField.MovementIntervalMin, movementIntervalMinSeconds, 1, 3600);
        CheckRange(errors, SettingsField.MovementIntervalMax, movementIntervalMaxSeconds, 1, 3600);
        CheckRange(errors, SettingsField.Amplitude, amplitudePixels, 10, 500);

        if (movementIntervalMinSeconds > movementIntervalMaxSeconds)
        {
            errors.Add(new SettingsError(SettingsField.MovementIntervalMin, SettingsErrorCode.MinGreaterThanMax));
        }

        if (errors.Count > 0)
        {
            return new ValidationResult<EngineSettings>(errors);
        }

        return new ValidationResult<EngineSettings>(new EngineSettings(
            TimeSpan.FromSeconds(idleThresholdSeconds),
            TimeSpan.FromSeconds(checkIntervalSeconds),
            TimeSpan.FromSeconds(movementIntervalMinSeconds),
            TimeSpan.FromSeconds(movementIntervalMaxSeconds),
            amplitudePixels,
            keepDisplayOn,
            keepAwakeWhileLocked));
    }

    private static void CheckRange(List<SettingsError> errors, SettingsField field, int value, int min, int max)
    {
        if (value < min || value > max)
        {
            errors.Add(new SettingsError(field, SettingsErrorCode.OutOfRange));
        }
    }
}
