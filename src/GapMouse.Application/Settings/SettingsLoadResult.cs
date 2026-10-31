namespace GapMouse.Application.Settings;

/// <summary>The outcome of loading settings: the effective settings and any recoverable problems found.</summary>
public sealed record SettingsLoadResult(AppSettings Settings, bool FileExisted, IReadOnlyList<string> Warnings);
