using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace GapMouse.App.Services;

/// <summary>Runs the ordered shutdown steps once, whatever the trigger (tray Exit, Home Exit, Windows session end).</summary>
public sealed partial class AppShutdown(IReadOnlyList<Action> steps, ILogger<AppShutdown> logger) : IAppLifetime
{
    private int _started;

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Every step must run even if an earlier one fails.")]
    public void Exit()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            return;
        }

        for (var i = 0; i < steps.Count; i++)
        {
            try
            {
                steps[i]();
            }
            catch (Exception ex)
            {
                LogStepFailed(ex, i);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Shutdown step {Index} failed")]
    private partial void LogStepFailed(Exception exception, int index);
}
