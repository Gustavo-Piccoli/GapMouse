using GapMouse.App;
using GapMouse.App.Composition;
using GapMouse.App.Services;
using GapMouse.App.ViewModels;
using GapMouse.Application.Paths;
using GapMouse.Application.Settings;
using GapMouse.Application.Shell;
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

    private sealed class FakeTrayIcon : ITrayIcon
    {
        public event EventHandler? OpenRequested { add { } remove { } }

        public event EventHandler? MenuRequested { add { } remove { } }

        public event EventHandler<string?>? BalloonClicked { add { } remove { } }

        public void SetState(TrayIconState state, string tooltip)
        {
        }

        public void ShowBalloon(string title, string message, string? tag, bool silent)
        {
        }

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
