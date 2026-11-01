using Avalonia.Controls;
using GapMouse.App.Localization;
using GapMouse.App.ViewModels;
using GapMouse.App.Views;

namespace GapMouse.App.Services;

/// <summary>Shows the custom pause dialog modally over the main window when it is visible, otherwise centered on screen.</summary>
public sealed class AvaloniaPausePrompt(Localizer localizer, Func<Window?> owner) : IPausePrompt
{
    public Task<int?> AskMinutesAsync()
    {
        var completion = new TaskCompletionSource<int?>();
        var window = new CustomPauseWindow { DataContext = new CustomPauseViewModel(localizer) };
        window.Closed += (_, _) => completion.TrySetResult(window.Result);

        if (owner() is { IsVisible: true } parent)
        {
            _ = window.ShowDialog(parent);
        }
        else
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            window.Show();
            window.Activate();
        }

        return completion.Task;
    }
}
