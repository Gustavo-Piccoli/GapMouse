namespace GapMouse.Application.Settings;

/// <summary>Persists <see cref="AppSettings"/>.</summary>
public interface ISettingsStore
{
    /// <summary>Loads the settings, falling back to defaults (with warnings) when the file is missing or invalid.</summary>
    SettingsLoadResult Load();

    void Save(AppSettings settings);
}
