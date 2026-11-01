namespace GapMouse.App.Localization;

public sealed partial class Localizer
{
    public string SettingsGroupMovement => T("Movement", "Movimento");

    public string SettingsGroupPower => T("Power", "Energia");

    public string SettingsGroupSystem => T("System", "Sistema");

    public string SettingsGroupAppearance => T("Appearance", "Aparência");

    public string SettingsGroupLogs => T("Logs", "Logs");

    public string SettingsIdle => T("Idle time before moving (s)", "Tempo ocioso antes de mover (s)");

    public string SettingsCheck => T("Check interval (s)", "Intervalo de verificação (s)");

    public string SettingsMin => T("Minimum movement interval (s)", "Intervalo mínimo entre movimentos (s)");

    public string SettingsMax => T("Maximum movement interval (s)", "Intervalo máximo entre movimentos (s)");

    public string SettingsAmplitude => T("Movement amplitude (px)", "Amplitude do movimento (px)");

    public string SettingsKeepDisplayOn => T("Keep the display on", "Manter a tela ligada");

    public string SettingsStartWithWindows => T("Start with Windows", "Iniciar com o Windows");

    public string SettingsStartMinimized => T("Start minimized to the tray", "Iniciar minimizado na bandeja");

    public string SettingsNotifications => T("Show notifications", "Mostrar notificações");

    public string SettingsCheckUpdates => T("Check for updates", "Verificar atualizações");

    public string SettingsHotkey => T("Pause/resume shortcut", "Atalho de pausar/retomar");

    public string SettingsHotkeyHint => T("Click the box and press the new shortcut.", "Clique na caixa e pressione o novo atalho.");

    public string SettingsTheme => T("Theme", "Tema");

    public string SettingsThemeDark => T("Dark", "Escuro");

    public string SettingsThemeLight => T("Light", "Claro");

    public string SettingsLanguage => T("Language", "Idioma");

    public string SettingsLanguageAuto => T("Automatic", "Automático");

    public string SettingsLanguagePortuguese => T("Português", "Português");

    public string SettingsLanguageEnglish => T("English", "English");

    public string SettingsSaveLogs => T("Save logs to a file", "Salvar logs em arquivo");

    public string SettingsRetention => T("Keep log files for (days)", "Manter arquivos de log por (dias)");

    public string SettingsSave => T("Save", "Salvar");

    public string SettingsRestoreDefaults => T("Restore defaults", "Restaurar padrões");

    public string SettingsHotkeyUnavailable => T(
        "Shortcut unavailable — another app may be using it.",
        "Atalho indisponível — outro programa pode estar usando.");

    public string SettingsSaved => T("Settings saved.", "Configurações salvas.");

    public string SettingsNotWritten => T(
        "Applied, but the settings file could not be written.",
        "Aplicadas, mas o arquivo de configurações não pôde ser gravado.");

    public string SettingsStartupFailed => T(
        "Saved, but start with Windows could not be changed.",
        "Salvas, mas não foi possível alterar o início com o Windows.");

    public string SettingsErrorNotNumber => T("Enter a whole number.", "Digite um número inteiro.");

    public string SettingsErrorMinGreaterThanMax => T(
        "The minimum must not be greater than the maximum.",
        "O mínimo não pode ser maior que o máximo.");

    public string SettingsErrorHotkey => T(
        "Invalid shortcut. Use Ctrl, Alt or Win plus a letter, digit or F1–F24.",
        "Atalho inválido. Use Ctrl, Alt ou Win mais uma letra, dígito ou F1–F24.");

    public string SettingsErrorRange(int min, int max) => T(
        $"Enter a value from {min} to {max}.",
        $"Digite um valor de {min} a {max}.");
}
