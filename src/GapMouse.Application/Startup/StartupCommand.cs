namespace GapMouse.Application.Startup;

public static class StartupCommand
{
    public static string For(string exePath) => "\"" + exePath + "\" --minimized";
}
