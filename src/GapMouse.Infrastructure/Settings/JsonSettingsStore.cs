using System.Globalization;
using System.Text.Json;
using GapMouse.Application.Settings;
using GapMouse.Domain.Settings;
using GapMouse.Infrastructure.IO;

namespace GapMouse.Infrastructure.Settings;

public sealed class JsonSettingsStore(string settingsFilePath) : ISettingsStore
{
    private const int SchemaVersion = 1;

    public SettingsLoadResult Load()
    {
        if (!File.Exists(settingsFilePath))
        {
            return new SettingsLoadResult(AppSettings.Default, false, []);
        }

        SettingsDocument? document;
        try
        {
            document = JsonSerializer.Deserialize(
                File.ReadAllText(settingsFilePath),
                SettingsJsonContext.Default.SettingsDocument);
        }
        catch (JsonException)
        {
            return Corrupt();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new SettingsLoadResult(
                AppSettings.Default,
                true,
                [$"Settings: file could not be read ({ex.Message}); using defaults."]);
        }

        if (document is null)
        {
            return Corrupt();
        }

        List<string> warnings = [];
        var defaults = AppSettings.Default;
        var system = document.System;
        var logs = document.Logs;

        var settings = new AppSettings
        {
            Engine = ReadEngine(document.Engine, warnings),
            StartWithWindows = system?.StartWithWindows ?? defaults.StartWithWindows,
            StartMinimized = system?.StartMinimized ?? defaults.StartMinimized,
            NotificationsEnabled = system?.Notifications ?? defaults.NotificationsEnabled,
            PauseHotkey = ReadHotkey(system?.PauseHotkey, defaults.PauseHotkey, warnings),
            CheckForUpdates = system?.CheckForUpdates ?? defaults.CheckForUpdates,
            Theme = ReadEnum(document.Appearance?.Theme, "theme", defaults.Theme, warnings),
            Language = ReadEnum(document.Appearance?.Language, "language", defaults.Language, warnings),
            SaveLogsToFile = logs?.SaveToFile ?? defaults.SaveLogsToFile,
            LogRetentionDays = ReadRetention(logs?.RetentionDays, defaults.LogRetentionDays, warnings),
            TrayHintShown = document.Ui?.TrayHintShown ?? defaults.TrayHintShown,
        };

        return new SettingsLoadResult(settings, true, warnings);
    }

    public void Save(AppSettings settings)
    {
        var engine = settings.Engine;
        var document = new SettingsDocument
        {
            SchemaVersion = SchemaVersion,
            Engine = new SettingsDocument.EngineSection
            {
                IdleThresholdSeconds = (int)engine.IdleThreshold.TotalSeconds,
                CheckIntervalSeconds = (int)engine.CheckInterval.TotalSeconds,
                MovementIntervalMinSeconds = (int)engine.MovementIntervalMin.TotalSeconds,
                MovementIntervalMaxSeconds = (int)engine.MovementIntervalMax.TotalSeconds,
                AmplitudePixels = engine.AmplitudePixels,
                KeepDisplayOn = engine.KeepDisplayOn,
            },
            System = new SettingsDocument.SystemSection
            {
                StartWithWindows = settings.StartWithWindows,
                StartMinimized = settings.StartMinimized,
                Notifications = settings.NotificationsEnabled,
                PauseHotkey = settings.PauseHotkey,
                CheckForUpdates = settings.CheckForUpdates,
            },
            Appearance = new SettingsDocument.AppearanceSection
            {
                Theme = settings.Theme.ToString(),
                Language = settings.Language.ToString(),
            },
            Logs = new SettingsDocument.LogsSection
            {
                SaveToFile = settings.SaveLogsToFile,
                RetentionDays = settings.LogRetentionDays,
            },
            Ui = new SettingsDocument.UiSection { TrayHintShown = settings.TrayHintShown },
        };

        AtomicFile.WriteAllText(
            settingsFilePath,
            JsonSerializer.Serialize(document, SettingsJsonContext.Default.SettingsDocument));
    }

    private static EngineSettings ReadEngine(SettingsDocument.EngineSection? section, List<string> warnings)
    {
        if (section is null)
        {
            return EngineSettings.Default;
        }

        var d = EngineSettings.Default;
        var result = EngineSettings.Create(
            section.IdleThresholdSeconds ?? (int)d.IdleThreshold.TotalSeconds,
            section.CheckIntervalSeconds ?? (int)d.CheckInterval.TotalSeconds,
            section.MovementIntervalMinSeconds ?? (int)d.MovementIntervalMin.TotalSeconds,
            section.MovementIntervalMaxSeconds ?? (int)d.MovementIntervalMax.TotalSeconds,
            section.AmplitudePixels ?? d.AmplitudePixels,
            section.KeepDisplayOn ?? d.KeepDisplayOn);

        if (result.IsValid)
        {
            return result.Value;
        }

        var summary = string.Join(", ", result.Errors.Select(e => $"{e.Field} {e.Code}"));
        warnings.Add($"Settings: engine section is not valid ({summary}); using engine defaults.");
        return d;
    }

    private static string ReadHotkey(string? value, string fallback, List<string> warnings)
    {
        if (value is null)
        {
            return fallback;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            warnings.Add($"Settings: pause hotkey is empty; using {fallback}.");
            return fallback;
        }

        return value;
    }

    private static int ReadRetention(int? value, int fallback, List<string> warnings)
    {
        if (value is null)
        {
            return fallback;
        }

        if (value < AppSettings.MinLogRetentionDays || value > AppSettings.MaxLogRetentionDays)
        {
            warnings.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"Settings: log retention {value} is not valid; using {fallback}."));
            return fallback;
        }

        return value.Value;
    }

    private static T ReadEnum<T>(string? value, string name, T fallback, List<string> warnings)
        where T : struct, Enum
    {
        if (value is null)
        {
            return fallback;
        }

        if (Enum.TryParse(value, ignoreCase: true, out T parsed) && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        warnings.Add($"Settings: {name} '{value}' is not valid; using {fallback}.");
        return fallback;
    }

    private SettingsLoadResult Corrupt()
    {
        var bad = settingsFilePath + ".bad";
        try
        {
            File.Move(settingsFilePath, bad, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new SettingsLoadResult(
                AppSettings.Default,
                true,
                ["Settings: file is not valid JSON and could not be moved aside; using defaults."]);
        }

        return new SettingsLoadResult(
            AppSettings.Default,
            true,
            [$"Settings: file is not valid JSON; using defaults. A copy was kept as {Path.GetFileName(bad)}."]);
    }
}
