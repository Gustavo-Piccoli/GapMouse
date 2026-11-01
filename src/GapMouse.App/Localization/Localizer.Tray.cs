namespace GapMouse.App.Localization;

public sealed partial class Localizer
{
    public string TrayStateWaiting => T("Waiting", "Aguardando");

    public string TrayStateMoving => T("Moving", "Movendo");

    public string TrayStatePaused => T("Paused", "Pausado");

    public string TrayKeepingAwake => T("Keeping your PC awake", "Mantendo seu PC ativo");

    public string TrayWelcomeBack => T("Welcome back", "Bem-vindo de volta");

    public string TrayResumed => T("Resumed", "Retomado");

    public string TrayMenuPause => T("Pause", "Pausar");

    public string TrayMenuResume => T("Resume", "Retomar");

    public string TrayMenuOpen => T("Open", "Abrir");

    public string TrayMenuExit => T("Exit", "Sair");

    public string TrayPause10 => T("10 min", "10 min");

    public string TrayPause30 => T("30 min", "30 min");

    public string TrayPause1h => T("1 h", "1 h");

    public string TrayPauseCustom => T("Custom…", "Personalizado…");

    public string TrayPauseNever => T("Never", "Nunca");

    public string TrayUpdateTitle => T("Update available", "Atualização disponível");

    public string TrayHint => T("GapMouse is still running in the tray", "O GapMouse continua rodando na bandeja");

    public string TrayTooltip(string state) => $"{AppName} — {state}";

    public string TrayUpdateBody(string version) =>
        T($"Version {version} is available.", $"Versão {version} disponível.");
}
