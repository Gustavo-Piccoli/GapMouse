using Avalonia;
using Avalonia.Controls;
using GapMouse.App.Composition;
using GapMouse.Application.Paths;
using GapMouse.Infrastructure.Settings;
using GapMouse.Infrastructure.Shell;

namespace GapMouse.App;

internal static class Program
{
    private const int SignalAttempts = 10;

    private static readonly TimeSpan SignalDelay = TimeSpan.FromMilliseconds(200);

    [STAThread]
    public static int Main(string[] args)
    {
        using var instance = SingleInstance.TryAcquire();
        if (instance is null)
        {
            SignalRunningInstance();
            return 0;
        }

        var options = StartupOptions.Parse(args);
        var paths = AppPathsResolver.Resolve(
            AppContext.BaseDirectory,
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            File.Exists);
        var loaded = new JsonSettingsStore(paths.SettingsFile).Load();
        var services = AppComposition.Build(paths, options, loaded);

        App.UseServices(services, options);
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
    }

    // The first instance may hold the mutex before its shell window exists, so retry briefly.
    private static void SignalRunningInstance()
    {
        for (var i = 0; i < SignalAttempts; i++)
        {
            if (SingleInstance.SignalExisting())
            {
                return;
            }

            Thread.Sleep(SignalDelay);
        }
    }
}
