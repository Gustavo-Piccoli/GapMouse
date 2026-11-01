using System.Net.Http;
using System.Reflection;
using Avalonia.Platform;
using GapMouse.App.Localization;
using GapMouse.App.Services;
using GapMouse.App.ViewModels;
using GapMouse.App.Views;
using GapMouse.Application.Engine;
using GapMouse.Application.Logging;
using GapMouse.Application.Paths;
using GapMouse.Application.Settings;
using GapMouse.Application.Shell;
using GapMouse.Application.Startup;
using GapMouse.Application.State;
using GapMouse.Application.Updates;
using GapMouse.Infrastructure.Logging;
using GapMouse.Infrastructure.Randomness;
using GapMouse.Infrastructure.Settings;
using GapMouse.Infrastructure.Shell;
using GapMouse.Infrastructure.Startup;
using GapMouse.Infrastructure.State;
using GapMouse.Infrastructure.Updates;
using GapMouse.Infrastructure.Win32;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GapMouse.App.Composition;

/// <summary>The composition root. Only explicit singleton factories, so trimming and Native AOT are safe.</summary>
public static class AppComposition
{
    public static IServiceProvider Build(
        AppPaths paths,
        StartupOptions options,
        SettingsLoadResult loaded,
        Action<IServiceCollection>? overrides = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(loaded);

        var services = new ServiceCollection();
        var informationalVersion = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(AppComposition).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "0.0.0";

        services.AddSingleton(paths);
        services.AddSingleton(options);
        services.AddSingleton(loaded);
        services.AddSingleton(TimeProvider.System);

        AddLogging(services, loaded);
        AddEngine(services, loaded);
        AddSettings(services);
        AddShell(services);
        AddUpdates(services, informationalVersion);
        AddUi(services, paths, informationalVersion);
        AddShutdown(services);

        overrides?.Invoke(services);
        return services.BuildServiceProvider();
    }

    private static void Logger<T>(IServiceCollection services) =>
        services.AddSingleton<ILogger<T>>(sp => new ActivityLogger<T>(sp.GetRequiredService<IActivityLog>(), TimeProvider.System));

    private static void AddLogging(IServiceCollection services, SettingsLoadResult loaded)
    {
        services.AddSingleton(sp =>
        {
            // Configured before anything can append, so retention and the on/off switch never run with defaults.
            var file = new FileActivityLog(sp.GetRequiredService<AppPaths>().LogsDirectory, sp.GetRequiredService<TimeProvider>());
            file.Configure(loaded.Settings.SaveLogsToFile, loaded.Settings.LogRetentionDays);
            return file;
        });
        services.AddSingleton<ILogFileOptions>(sp => sp.GetRequiredService<FileActivityLog>());
        services.AddSingleton<InMemoryActivityLog>();
        services.AddSingleton<IActivityLog>(sp =>
        {
            var composite = new CompositeActivityLog([sp.GetRequiredService<FileActivityLog>(), sp.GetRequiredService<InMemoryActivityLog>()]);
            var now = sp.GetRequiredService<TimeProvider>().GetLocalNow();
            foreach (var warning in loaded.Warnings)
            {
                composite.Append(new LogEntry(now, LogKind.Warn, warning));
            }

            return composite;
        });

        Logger<EngineHost>(services);
        Logger<AppStatePersister>(services);
        Logger<UpdateService>(services);
        Logger<SettingsService>(services);
        Logger<StartWithWindowsService>(services);
        Logger<AppShutdown>(services);
        Logger<ShellWindow>(services);
        Logger<App>(services);
    }

    private static void AddEngine(IServiceCollection services, SettingsLoadResult loaded)
    {
        services.AddSingleton<IAppStateStore>(sp => new JsonAppStateStore(sp.GetRequiredService<AppPaths>().StateFile));
        services.AddSingleton(sp =>
        {
            var time = sp.GetRequiredService<TimeProvider>();
            var today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);
            var restored = sp.GetRequiredService<IAppStateStore>().Load()?.TodayOrNull(today);
            return new EngineHost(
                loaded.Settings.Engine,
                new Win32IdleTimeProvider(),
                new Win32CursorController(),
                new Win32MonitorBoundsProvider(),
                new Win32PowerRequestService(),
                new SystemRandomSource(),
                time,
                sp.GetRequiredService<ILogger<EngineHost>>(),
                restored);
        });
        services.AddSingleton<IEngineController>(sp => sp.GetRequiredService<EngineHost>());
        services.AddSingleton(sp => new ActivityLogWriter(sp.GetRequiredService<IEngineController>(), sp.GetRequiredService<IActivityLog>()));
        services.AddSingleton(sp => new AppStatePersister(
            sp.GetRequiredService<IEngineController>(),
            sp.GetRequiredService<IAppStateStore>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ILogger<AppStatePersister>>()));
    }

    private static void AddSettings(IServiceCollection services)
    {
        services.AddSingleton<ISettingsStore>(sp => new JsonSettingsStore(sp.GetRequiredService<AppPaths>().SettingsFile));
        services.AddSingleton<IStartupRegistrar>(_ => new RegistryStartupRegistrar());
        services.AddSingleton(sp => new StartWithWindowsService(
            sp.GetRequiredService<IStartupRegistrar>(),
            Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "GapMouse.exe"),
            sp.GetRequiredService<ILogger<StartWithWindowsService>>()));
        services.AddSingleton<ISettingsService>(sp => new SettingsService(
            sp.GetRequiredService<SettingsLoadResult>(),
            sp.GetRequiredService<ISettingsStore>(),
            sp.GetRequiredService<IEngineController>(),
            sp.GetRequiredService<ILogFileOptions>(),
            sp.GetRequiredService<StartWithWindowsService>(),
            sp.GetRequiredService<IGlobalHotkey>(),
            sp.GetRequiredService<ILogger<SettingsService>>()));
    }

    private static void AddShell(IServiceCollection services)
    {
        services.AddSingleton(_ => new ShellWindow());
        services.AddSingleton<ITrayIcon>(sp => new Win32TrayIcon(sp.GetRequiredService<ShellWindow>(), LoadTrayIcons()));
        services.AddSingleton<IGlobalHotkey>(sp => new Win32GlobalHotkey(sp.GetRequiredService<ShellWindow>()));
    }

    private static Dictionary<TrayIconState, byte[]> LoadTrayIcons() => new()
    {
        [TrayIconState.Waiting] = ReadAsset("tray-waiting.ico"),
        [TrayIconState.Moving] = ReadAsset("tray-moving.ico"),
        [TrayIconState.Paused] = ReadAsset("tray-paused.ico"),
    };

    private static byte[] ReadAsset(string name)
    {
        using var stream = AssetLoader.Open(new Uri($"avares://GapMouse/Assets/Icons/{name}"));
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static void AddUpdates(IServiceCollection services, string informationalVersion)
    {
        services.AddSingleton(_ => new HttpClient());
        services.AddSingleton<IReleaseSource>(sp => new GitHubReleaseSource(sp.GetRequiredService<HttpClient>(), informationalVersion));
        services.AddSingleton(sp =>
        {
            var current = ReleaseVersion.TryParse(informationalVersion, out var parsed) ? parsed : new Version(0, 0, 0);
            return new UpdateService(
                sp.GetRequiredService<IReleaseSource>(),
                current,
                sp.GetRequiredService<TimeProvider>(),
                sp.GetRequiredService<ILogger<UpdateService>>());
        });
        services.AddSingleton<IUpdateChecker>(sp => sp.GetRequiredService<UpdateService>());
    }

    private static void AddUi(IServiceCollection services, AppPaths paths, string informationalVersion)
    {
        // The About tab shows the version without the "+<commit>" build metadata.
        var displayVersion = informationalVersion.Split('+')[0];

        services.AddSingleton(_ => new Localizer());
        services.AddSingleton<IUiDispatcher>(_ => new AvaloniaUiDispatcher());
        services.AddSingleton<IShellLauncher>(_ => new ShellLauncher());
        services.AddSingleton<IClipboardService>(sp =>
            new AvaloniaClipboardService(() => sp.GetRequiredService<MainWindowService>().Window));
        services.AddSingleton<IPausePrompt>(sp =>
            new AvaloniaPausePrompt(sp.GetRequiredService<Localizer>(), () => sp.GetRequiredService<MainWindowService>().Window));
        services.AddSingleton(sp => new ThemeApplier(sp.GetRequiredService<Localizer>(), sp.GetRequiredService<ISettingsService>()));

        services.AddSingleton(sp => new HomeViewModel(
            sp.GetRequiredService<Localizer>(),
            sp.GetRequiredService<IEngineController>(),
            sp.GetRequiredService<IUiDispatcher>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<IPausePrompt>(),
            sp.GetRequiredService<IAppLifetime>()));
        services.AddSingleton(sp => new LogsViewModel(
            sp.GetRequiredService<Localizer>(),
            sp.GetRequiredService<InMemoryActivityLog>(),
            sp.GetRequiredService<IUiDispatcher>(),
            sp.GetRequiredService<IClipboardService>(),
            sp.GetRequiredService<IShellLauncher>(),
            paths.LogsDirectory));
        services.AddSingleton(sp => new SettingsViewModel(sp.GetRequiredService<Localizer>(), sp.GetRequiredService<ISettingsService>()));
        services.AddSingleton(sp => new AboutViewModel(
            sp.GetRequiredService<Localizer>(),
            sp.GetRequiredService<IUpdateChecker>(),
            sp.GetRequiredService<IShellLauncher>(),
            sp.GetRequiredService<IUiDispatcher>(),
            paths,
            displayVersion));
        services.AddSingleton(sp => new MainWindowViewModel(
            sp.GetRequiredService<Localizer>(),
            sp.GetRequiredService<HomeViewModel>(),
            sp.GetRequiredService<LogsViewModel>(),
            sp.GetRequiredService<SettingsViewModel>(),
            sp.GetRequiredService<AboutViewModel>()));
        services.AddSingleton(sp => new TrayCoordinator(
            sp.GetRequiredService<Localizer>(),
            sp.GetRequiredService<IEngineController>(),
            sp.GetRequiredService<ITrayIcon>(),
            sp.GetRequiredService<IGlobalHotkey>(),
            sp.GetRequiredService<ISettingsService>(),
            sp.GetRequiredService<IUpdateChecker>(),
            sp.GetRequiredService<IPausePrompt>(),
            sp.GetRequiredService<IMainWindowService>(),
            sp.GetRequiredService<IAppLifetime>(),
            sp.GetRequiredService<IShellLauncher>(),
            sp.GetRequiredService<IUiDispatcher>()));

        services.AddSingleton<Func<MainWindow>>(sp => () => new MainWindow { DataContext = sp.GetRequiredService<MainWindowViewModel>() });
        services.AddSingleton(sp => new MainWindowService(sp.GetRequiredService<Func<MainWindow>>(), sp.GetRequiredService<TrayCoordinator>));
        services.AddSingleton<IMainWindowService>(sp => sp.GetRequiredService<MainWindowService>());
    }

    private static void AddShutdown(IServiceCollection services)
    {
        services.AddSingleton<IAppLifetime>(sp => sp.GetRequiredService<AppShutdown>());
        services.AddSingleton(sp =>
        {
            // Resolved at exit time, not now: the tray coordinator and view models depend on IAppLifetime.
            T Get<T>()
                where T : notnull => sp.GetRequiredService<T>();

            Action[] steps =
            [
                () => Get<HomeViewModel>().Dispose(),
                () => Get<LogsViewModel>().Dispose(),
                () => Get<SettingsViewModel>().Dispose(),
                () => Get<AboutViewModel>().Dispose(),
                () => Get<TrayCoordinator>().Dispose(),
                () => Get<ITrayIcon>().Dispose(),
                () => Get<IGlobalHotkey>().Dispose(),
                () => Get<EngineHost>().Dispose(),
                () => Get<AppStatePersister>().Dispose(),
                () => Get<ActivityLogWriter>().Dispose(),
                () => Get<UpdateService>().Dispose(),
                () => Get<HttpClient>().Dispose(),
                () => Get<ShellWindow>().Dispose(),
                RequestDesktopShutdown,
            ];
            return new AppShutdown(steps, sp.GetRequiredService<ILogger<AppShutdown>>());
        });
    }

    private static void RequestDesktopShutdown()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IControlledApplicationLifetime lifetime)
        {
            lifetime.Shutdown();
        }
    }
}
