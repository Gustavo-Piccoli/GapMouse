using Microsoft.Extensions.Logging;

namespace GapMouse.App.Composition;

internal static partial class AppLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Shell window handler failed")]
    public static partial void ShellHandlerFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Session lock notifications unavailable; the mouse keeps moving while locked")]
    public static partial void SessionNotificationsUnavailable(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception on the UI thread")]
    public static partial void UnhandledUiException(ILogger logger, Exception exception);
}
