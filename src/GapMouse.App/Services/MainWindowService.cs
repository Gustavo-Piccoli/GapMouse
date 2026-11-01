using Avalonia.Controls;
using GapMouse.App.ViewModels;
using GapMouse.App.Views;

namespace GapMouse.App.Services;

/// <summary>Creates the main window lazily and turns its close button into "hide to tray".</summary>
/// <remarks>The tray coordinator is taken as a factory because it depends on this service.</remarks>
public sealed class MainWindowService(Func<MainWindow> factory, Func<TrayCoordinator> tray) : IMainWindowService
{
    private MainWindow? _window;

    /// <summary>Gets the window if it has been created.</summary>
    public MainWindow? Window => _window;

    public void Show()
    {
        var window = _window ??= Create();
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Show();
        window.Activate();
        window.Topmost = true;
        window.Topmost = false;
    }

    private MainWindow Create()
    {
        var window = factory();
        window.Closing += OnClosing;
        return window;
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (e.CloseReason != WindowCloseReason.WindowClosing || sender is not Window window)
        {
            return;
        }

        e.Cancel = true;
        window.Hide();
        tray().OnMainWindowHidden();
    }
}
