namespace GapMouse.Application.Settings;

/// <summary>The outcome of an explicit settings save.</summary>
public sealed record SettingsSaveResult(bool Saved, bool StartupApplied, bool HotkeyRegistered);
