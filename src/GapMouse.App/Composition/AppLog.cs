using Microsoft.Extensions.Logging;

namespace GapMouse.App.Composition;

internal static partial class AppLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Shell window handler failed")]
    public static partial void ShellHandlerFailed(ILogger logger, Exception exception);
}
