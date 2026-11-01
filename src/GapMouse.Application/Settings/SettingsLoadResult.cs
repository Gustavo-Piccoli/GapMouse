namespace GapMouse.Application.Settings;

/// <summary>The outcome of loading settings: the effective settings and any recoverable problems found.</summary>
public sealed record SettingsLoadResult(AppSettings Settings, bool FileExisted, IReadOnlyList<string> Warnings)
{
    /// <summary>True when the file exists but could not be read (IO or access error); automatic saves must not overwrite it.</summary>
    public bool ReadFailed { get; init; }
}
