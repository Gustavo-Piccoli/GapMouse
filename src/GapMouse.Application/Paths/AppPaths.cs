namespace GapMouse.Application.Paths;

/// <summary>Where the app keeps its configuration, state and logs.</summary>
public sealed record AppPaths(string ConfigDirectory, string LogsDirectory, bool IsPortable)
{
    public const string SettingsFileName = "GapMouse.settings.json";

    public const string StateFileName = "GapMouse.state.json";

    public string SettingsFile => Path.Combine(ConfigDirectory, SettingsFileName);

    public string StateFile => Path.Combine(ConfigDirectory, StateFileName);

    public static AppPaths Portable(string exeDirectory) =>
        new(exeDirectory, Path.Combine(exeDirectory, "logs"), IsPortable: true);
}
