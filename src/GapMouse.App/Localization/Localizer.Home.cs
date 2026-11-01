using System.Globalization;

namespace GapMouse.App.Localization;

public sealed partial class Localizer
{
    public string HomeStateWaiting => T("Waiting", "Aguardando");

    public string HomeStateMoving => T("Moving", "Movendo");

    public string HomeStatePaused => T("Paused", "Pausado");

    public string HomePausedUntilResume => T("Paused until you resume", "Pausado até você retomar");

    public string HomeStartedLabel => T("Started", "Iniciado");

    public string HomeLastMovementLabel => T("Last movement", "Último movimento");

    public string HomeLastReturnLabel => T("Last return", "Último retorno");

    public string HomeLastPauseLabel => T("Last pause", "Última pausa");

    public string HomeLastResumeLabel => T("Last resume", "Última retomada");

    public string HomeTodayTitle => T("Today", "Hoje");

    public string HomeRecentTitle => T("Recent", "Recentes");

    public string HomeActiveTimeLabel => T("Active time", "Tempo ativo");

    public string HomeActivationsLabel => T("Activations", "Ativações");

    public string HomePause => T("Pause", "Pausar");

    public string HomeResume => T("Resume", "Retomar");

    public string HomeExit => T("Exit application", "Sair do aplicativo");

    public string PauseTenMinutes => T("10 minutes", "10 minutos");

    public string PauseThirtyMinutes => T("30 minutes", "30 minutos");

    public string PauseOneHour => T("1 hour", "1 hora");

    public string PauseCustom => T("Custom…", "Personalizado…");

    public string PauseNever => T("Until I resume", "Até eu retomar");

    public string CustomPauseTitle => T("Custom pause", "Pausa personalizada");

    public string CustomPausePrompt => T("Pause for how many minutes? (1–1440)", "Pausar por quantos minutos? (1–1440)");

    public string CustomPauseInvalid => T("Enter a whole number from 1 to 1440.", "Informe um número inteiro de 1 a 1440.");

    public string FormatIdle(int seconds, int thresholdSeconds) =>
        string.Format(Culture, T("Idle for {0}\u00A0s of {1}\u00A0s", "Inativo há {0}\u00A0s de {1}\u00A0s"), seconds, thresholdSeconds);

    public string FormatResumesIn(string time) =>
        string.Format(Culture, T("Resumes in {0}", "Retoma em {0}"), time);
}
