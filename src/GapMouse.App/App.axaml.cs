using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using GapMouse.App.Localization;
using GapMouse.App.ViewModels;
using GapMouse.App.Views;
using GapMouse.Application.Settings;

namespace GapMouse.App;

public sealed class App : Avalonia.Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Placeholder composition until the composition root arrives (Task 10).
            var localizer = new Localizer();
            var window = new MainWindow
            {
                DataContext = new MainWindowViewModel(
                    localizer,
                    new HomeViewModel(localizer, null!, null!, TimeProvider.System, null!, null!),
                    new LogsViewModel(
                        localizer,
                        new GapMouse.Application.Logging.InMemoryActivityLog(),
                        new Services.AvaloniaUiDispatcher(),
                        new Services.AvaloniaClipboardService(() => null),
                        new Services.ShellLauncher(),
                        string.Empty),
                    new SettingsViewModel(localizer, new PlaceholderSettingsService()),
                    new AboutViewModel(
                        localizer,
                        new PlaceholderUpdateChecker(),
                        new Services.ShellLauncher(),
                        new Services.AvaloniaUiDispatcher(),
                        new GapMouse.Application.Paths.AppPaths(string.Empty, string.Empty, IsPortable: true),
                        "0.1.0")),
            };
            window.Closed += (_, _) => desktop.Shutdown();
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}

internal sealed class PlaceholderUpdateChecker : GapMouse.Application.Updates.IUpdateChecker
{
    public event EventHandler<GapMouse.Application.Updates.UpdateCheckResult>? UpdateFound
    {
        add { }
        remove { }
    }

    public Task<GapMouse.Application.Updates.UpdateCheckResult> CheckNowAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new GapMouse.Application.Updates.UpdateCheckResult(GapMouse.Application.Updates.UpdateCheckOutcome.Failed, null, null));
}

// Placeholder until the composition root arrives (Task 10).
internal sealed class PlaceholderSettingsService : ISettingsService
{
    public AppSettings Current { get; private set; } = AppSettings.Default;

    public bool IsHotkeyRegistered => true;

    public void Initialize()
    {
    }

    public SettingsSaveResult Save(AppSettings settings)
    {
        Current = settings;
        return new SettingsSaveResult(true, true, true);
    }

    public void MarkTrayHintShown()
    {
    }

    public event EventHandler<AppSettings>? Changed
    {
        add { }
        remove { }
    }
}
