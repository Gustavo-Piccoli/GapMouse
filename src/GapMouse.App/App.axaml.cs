using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using GapMouse.App.Composition;
using GapMouse.App.Services;
using GapMouse.App.ViewModels;
using GapMouse.Application.Engine;
using GapMouse.Application.Logging;
using GapMouse.Application.Settings;
using GapMouse.Application.State;
using GapMouse.Application.Updates;
using GapMouse.Infrastructure.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GapMouse.App;

public sealed class App : Avalonia.Application
{
    private static IServiceProvider? s_services;
    private static StartupOptions? s_options;

    /// <summary>Hands the composed services to the app; Avalonia creates the instance through its parameterless constructor.</summary>
    internal static void UseServices(IServiceProvider services, StartupOptions options)
    {
        s_services = services;
        s_options = options;
    }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
        {
            Compose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void Compose()
    {
        var services = s_services ?? throw new InvalidOperationException("Services were not composed.");
        var options = s_options ?? throw new InvalidOperationException("Options were not parsed.");
        var host = services.GetRequiredService<EngineHost>();
        services.GetRequiredService<ActivityLogWriter>();
        var persister = services.GetRequiredService<AppStatePersister>();
        persister.Start();
        host.Start();

        var shell = services.GetRequiredService<ShellWindow>();

        var settings = services.GetRequiredService<ISettingsService>();
        settings.Initialize();
        services.GetRequiredService<ThemeApplier>().Start();

        // Every view model exists up front so shutdown disposes each one exactly once.
        _ = services.GetRequiredService<MainWindowViewModel>();
        services.GetRequiredService<TrayCoordinator>().Start();
        services.GetRequiredService<HomeViewModel>().Start();
        services.GetRequiredService<UpdateService>().Start(() => settings.Current.CheckForUpdates);

        var windows = services.GetRequiredService<IMainWindowService>();
        var shellLogger = services.GetRequiredService<ILogger<ShellWindow>>();
        shell.ActivationRequested += (_, _) => windows.Show();
        shell.SessionEnding += (_, _) => services.GetRequiredService<IAppLifetime>().Exit();
        shell.HandlerFailed += ex => AppLog.ShellHandlerFailed(shellLogger, ex);

        if (!(options.Minimized || settings.Current.StartMinimized))
        {
            windows.Show();
        }
    }
}
