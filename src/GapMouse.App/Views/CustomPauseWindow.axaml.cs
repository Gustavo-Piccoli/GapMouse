using Avalonia.Controls;
using Avalonia.Interactivity;
using GapMouse.App.ViewModels;

namespace GapMouse.App.Views;

public sealed partial class CustomPauseWindow : Window
{
    public CustomPauseWindow() => InitializeComponent();

    /// <summary>Gets the confirmed whole minutes, or null when cancelled or closed.</summary>
    public int? Result { get; private set; }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CustomPauseViewModel { Minutes: { } minutes })
        {
            Result = minutes;
            Close();
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
