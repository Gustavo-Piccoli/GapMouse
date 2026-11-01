namespace GapMouse.Application.Settings;

/// <summary>Owns the current settings, persists them and applies them to the running app.</summary>
public interface ISettingsService
{
    AppSettings Current { get; }

    bool IsHotkeyRegistered { get; }

    void Initialize();

    SettingsSaveResult Save(AppSettings settings);

    void MarkTrayHintShown();

    event EventHandler<AppSettings>? Changed;
}
