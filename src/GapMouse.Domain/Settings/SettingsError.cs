namespace GapMouse.Domain.Settings;

public readonly record struct SettingsError(SettingsField Field, SettingsErrorCode Code);
