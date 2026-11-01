using System.Globalization;

namespace GapMouse.App.Localization;

public sealed partial class Localizer
{
    public string LogsFilterAll => T("All", "Todos");

    public string LogsFilterMovement => T("Movement", "Movimento");

    public string LogsFilterPauses => T("Pauses", "Pausas");

    public string LogsFilterSystem => T("System", "Sistema");

    public string LogsCopy => T("Copy", "Copiar");

    public string LogsOpenFolder => T("Open folder", "Abrir pasta");

    public string LogsClear => T("Clear", "Limpar");

    public string LogsEmpty => T(
        "No activity yet. Every start, movement, return and pause will appear here.",
        "Nenhuma atividade ainda. Cada início, movimento, retorno e pausa aparece aqui.");

    public string LogEngineStarted => T("GapMouse started", "GapMouse iniciado");

    public string LogEngineStopped => T("GapMouse stopped", "GapMouse encerrado");

    public string LogUserReturned => T("User activity detected", "Atividade do usuário detectada");

    public string LogSettingsChanged => T("Settings changed", "Configurações alteradas");

    public string LogPausedIndefinitely => T("Paused indefinitely", "Pausado por tempo indeterminado");

    public string LogResumedManual => T("Resumed (manual)", "Retomado (manual)");

    public string LogResumedExpired => T("Resumed (pause expired)", "Retomado (tempo esgotado)");

    public string LogWaitingStarted(TimeSpan idle) =>
        string.Format(Culture, T("Waiting for {0} s of inactivity", "Aguardando {0} s de inatividade"), Seconds(idle));

    public string LogMovingStarted(TimeSpan min, TimeSpan max) =>
        string.Format(Culture, T("Moving the mouse every {0}–{1} s", "Movendo o mouse a cada {0}–{1} s"), Seconds(min), Seconds(max));

    public string LogPausedUntil(DateTimeOffset until) =>
        string.Format(Culture, T("Paused until {0}", "Pausado até {0}"), until.ToString("HH:mm:ss", CultureInfo.InvariantCulture));

    private string Seconds(TimeSpan value) => value.TotalSeconds.ToString("0.##", Culture);
}
