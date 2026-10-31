namespace GapMouse.Application.Paths;

/// <summary>Chooses between portable and per-user data locations.</summary>
public static class AppPathsResolver
{
    private const string PortableMarker = "portable";

    public static AppPaths Resolve(string exeDirectory, string roamingAppData, string localAppData, Func<string, bool> fileExists)
    {
        ArgumentNullException.ThrowIfNull(fileExists);

        if (fileExists(Path.Combine(exeDirectory, PortableMarker))
            || fileExists(Path.Combine(exeDirectory, AppPaths.SettingsFileName)))
        {
            return AppPaths.Portable(exeDirectory);
        }

        return new AppPaths(
            Path.Combine(roamingAppData, "GapMouse"),
            Path.Combine(localAppData, "GapMouse", "logs"),
            IsPortable: false);
    }
}
