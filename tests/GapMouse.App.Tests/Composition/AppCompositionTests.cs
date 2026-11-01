using System.Globalization;
using System.Net.Http;
using GapMouse.App;
using GapMouse.App.Composition;
using GapMouse.App.Services;
using GapMouse.App.ViewModels;
using GapMouse.Application.Engine;
using GapMouse.Application.Logging;
using GapMouse.Application.Paths;
using GapMouse.Application.Settings;
using GapMouse.Application.Shell;
using GapMouse.Application.State;
using GapMouse.Application.Updates;
using GapMouse.Testing.IO;
using Microsoft.Extensions.DependencyInjection;

namespace GapMouse.App.Tests.Composition;

public sealed class AppCompositionTests
{
    [Fact]
    public void Build_WithFakeShell_ResolvesEveryViewModelAndService()
    {
        using var temp = new TempDirectory();
        var paths = AppPaths.Portable(temp.Path);
        var loaded = new SettingsLoadResult(AppSettings.Default, FileExisted: false, ["sample warning"]);

        using var provider = (ServiceProvider)AppComposition.Build(
            paths,
            new StartupOptions(Minimized: true),
            loaded,
            services =>
            {
                services.AddSingleton<ITrayIcon, FakeTrayIcon>();
                services.AddSingleton<IGlobalHotkey, FakeHotkey>();
            });

        provider.GetRequiredService<MainWindowViewModel>().ShouldNotBeNull();
        provider.GetRequiredService<HomeViewModel>().ShouldNotBeNull();
        provider.GetRequiredService<LogsViewModel>().ShouldNotBeNull();
        provider.GetRequiredService<SettingsViewModel>().ShouldNotBeNull();
        provider.GetRequiredService<AboutViewModel>().VersionText.ShouldNotContain("+");
        provider.GetRequiredService<TrayCoordinator>().ShouldNotBeNull();
        provider.GetRequiredService<ISettingsService>().ShouldNotBeNull();
        provider.GetRequiredService<IMainWindowService>().ShouldNotBeNull();
        provider.GetRequiredService<IAppLifetime>().ShouldNotBeNull();
        provider.GetRequiredService<ThemeApplier>().ShouldNotBeNull();
    }

    [Fact]
    public void Build_ResolvesEveryTypeUsedByTheShutdownSteps()
    {
        using var temp = new TempDirectory();
        using var provider = BuildProvider(temp, AppSettings.Default);

        provider.GetRequiredService<HomeViewModel>().ShouldNotBeNull();
        provider.GetRequiredService<LogsViewModel>().ShouldNotBeNull();
        provider.GetRequiredService<SettingsViewModel>().ShouldNotBeNull();
        provider.GetRequiredService<AboutViewModel>().ShouldNotBeNull();
        provider.GetRequiredService<TrayCoordinator>().ShouldNotBeNull();
        provider.GetRequiredService<ITrayIcon>().ShouldNotBeNull();
        provider.GetRequiredService<IGlobalHotkey>().ShouldNotBeNull();
        provider.GetRequiredService<EngineHost>().ShouldNotBeNull();
        provider.GetRequiredService<AppStatePersister>().ShouldNotBeNull();
        provider.GetRequiredService<ActivityLogWriter>().ShouldNotBeNull();
        provider.GetRequiredService<UpdateService>().ShouldNotBeNull();
        provider.GetRequiredService<HttpClient>().ShouldNotBeNull();
        provider.GetRequiredService<AppShutdown>().ShouldNotBeNull();
    }

    [Fact]
    public void Log_ConfiguredFromLoadedSettings_KeepsFilesWithinRetention()
    {
        using var temp = new TempDirectory();
        var paths = AppPaths.Portable(temp.Path);
        Directory.CreateDirectory(paths.LogsDirectory);
        var old = DateTime.Now.AddDays(-20).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var oldFile = Path.Combine(paths.LogsDirectory, $"gapmouse-{old}.log");
        File.WriteAllText(oldFile, "old");
        using var provider = BuildProvider(temp, new AppSettings { LogRetentionDays = 30 });

        provider.GetRequiredService<IActivityLog>().Append(new LogEntry(DateTimeOffset.Now, LogKind.Start, "hello"));

        File.Exists(oldFile).ShouldBeTrue();
    }

    [Fact]
    public void Log_FileLoggingDisabled_WritesNothingToDisk()
    {
        using var temp = new TempDirectory();
        var paths = AppPaths.Portable(temp.Path);
        using var provider = BuildProvider(temp, new AppSettings { SaveLogsToFile = false });

        provider.GetRequiredService<IActivityLog>().Append(new LogEntry(DateTimeOffset.Now, LogKind.Start, "hello"));

        (!Directory.Exists(paths.LogsDirectory) || Directory.GetFiles(paths.LogsDirectory).Length == 0).ShouldBeTrue();
    }

    private static ServiceProvider BuildProvider(TempDirectory temp, AppSettings settings) =>
        (ServiceProvider)AppComposition.Build(
            AppPaths.Portable(temp.Path),
            new StartupOptions(Minimized: true),
            new SettingsLoadResult(settings, FileExisted: false, []),
            services =>
            {
                services.AddSingleton<ITrayIcon, FakeTrayIcon>();
                services.AddSingleton<IGlobalHotkey, FakeHotkey>();
            });

    private sealed class FakeTrayIcon : ITrayIcon
    {
        public event EventHandler? OpenRequested { add { } remove { } }

        public event EventHandler? MenuRequested { add { } remove { } }

        public event EventHandler<string?>? BalloonClicked { add { } remove { } }

        public void SetState(TrayIconState state, string tooltip)
        {
        }

        public bool ShowBalloon(string title, string message, string? tag, bool silent) => true;

        public string? ShowMenu(IReadOnlyList<TrayMenuItem> items) => null;

        public void Dispose()
        {
        }
    }

    private sealed class FakeHotkey : IGlobalHotkey
    {
        public event EventHandler? Pressed { add { } remove { } }

        public bool TryRegister(HotkeyGesture gesture) => true;

        public void Unregister()
        {
        }

        public void Dispose()
        {
        }
    }
}
