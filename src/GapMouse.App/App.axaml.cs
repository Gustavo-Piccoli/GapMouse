using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using GapMouse.App.Localization;
using GapMouse.App.ViewModels;
using GapMouse.App.Views;

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
                    new HomeViewModel(localizer),
                    new LogsViewModel(localizer),
                    new SettingsViewModel(localizer),
                    new AboutViewModel(localizer)),
            };
            window.Closed += (_, _) => desktop.Shutdown();
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
