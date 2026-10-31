using System.Security;
using Microsoft.Extensions.Logging;

namespace GapMouse.Application.Startup;

public sealed partial class StartWithWindowsService(
    IStartupRegistrar registrar,
    string exePath,
    ILogger<StartWithWindowsService> logger)
{
    public bool InitialPreference(bool settingsFileExisted, bool savedPreference)
    {
        if (settingsFileExisted)
        {
            return savedPreference;
        }

        try
        {
            return registrar.GetCommand() is not null;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or SecurityException)
        {
            LogReadFailed(ex);
            return false;
        }
    }

    public bool Apply(bool enabled)
    {
        try
        {
            var current = registrar.GetCommand();
            if (enabled)
            {
                var wanted = StartupCommand.For(exePath);
                if (!string.Equals(current, wanted, StringComparison.OrdinalIgnoreCase))
                {
                    registrar.SetCommand(wanted);
                }
            }
            else if (current is not null)
            {
                registrar.Remove();
            }

            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or SecurityException)
        {
            LogApplyFailed(ex, enabled);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read the start with Windows registration")]
    private partial void LogReadFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not apply start with Windows (enabled: {Enabled})")]
    private partial void LogApplyFailed(Exception exception, bool enabled);
}
