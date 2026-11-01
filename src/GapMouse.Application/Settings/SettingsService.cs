using GapMouse.Application.Engine;
using GapMouse.Application.Logging;
using GapMouse.Application.Shell;
using GapMouse.Application.Startup;
using Microsoft.Extensions.Logging;

namespace GapMouse.Application.Settings;

/// <summary>Owns the current settings, saves them and applies changes to the running app without a restart.</summary>
public sealed partial class SettingsService(
    SettingsLoadResult loaded,
    ISettingsStore store,
    IEngineController engine,
    ILogFileOptions logs,
    StartWithWindowsService startup,
    IGlobalHotkey hotkey,
    ILogger<SettingsService> logger) : ISettingsService
{
    private bool _readFailed = loaded.ReadFailed;
    private string? _registeredHotkeyText;

    public AppSettings Current { get; private set; } = loaded.Settings;

    public bool IsHotkeyRegistered { get; private set; }

    public event EventHandler<AppSettings>? Changed;

    public void Initialize()
    {
        var effective = startup.InitialPreference(loaded.FileExisted, loaded.Settings.StartWithWindows);
        Current = loaded.Settings with { StartWithWindows = effective };

        startup.Apply(effective);
        logs.Configure(Current.SaveLogsToFile, Current.LogRetentionDays);
        RegisterHotkey(Current.PauseHotkey);
    }

    public SettingsSaveResult Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.Engine != Current.Engine)
        {
            engine.ApplySettings(settings.Engine);
        }

        logs.Configure(settings.SaveLogsToFile, settings.LogRetentionDays);
        var startupApplied = startup.Apply(settings.StartWithWindows);

        if (!IsHotkeyRegistered || !string.Equals(settings.PauseHotkey, _registeredHotkeyText, StringComparison.Ordinal))
        {
            RegisterHotkey(settings.PauseHotkey);
        }

        Current = settings;
        var saved = TryPersist(settings);
        if (saved)
        {
            _readFailed = false;
        }

        Changed?.Invoke(this, Current);
        return new SettingsSaveResult(saved, startupApplied, IsHotkeyRegistered);
    }

    public void MarkTrayHintShown()
    {
        Current = Current with { TrayHintShown = true };
        if (!_readFailed)
        {
            TryPersist(Current);
        }

        Changed?.Invoke(this, Current);
    }

    private bool TryPersist(AppSettings settings)
    {
        try
        {
            store.Save(settings);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogSaveFailed(ex);
            return false;
        }
    }

    private void RegisterHotkey(string text)
    {
        _registeredHotkeyText = text;
        if (!HotkeyGesture.TryParse(text, out var gesture))
        {
            hotkey.Unregister();
            IsHotkeyRegistered = false;
            LogHotkeyInvalid(text);
            return;
        }

        IsHotkeyRegistered = hotkey.TryRegister(gesture);
        if (!IsHotkeyRegistered)
        {
            LogHotkeyFailed(text);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not save settings; keeping them for this session")]
    private partial void LogSaveFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Pause hotkey '{Hotkey}' is not a valid gesture")]
    private partial void LogHotkeyInvalid(string hotkey);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not register pause hotkey '{Hotkey}'")]
    private partial void LogHotkeyFailed(string hotkey);
}
