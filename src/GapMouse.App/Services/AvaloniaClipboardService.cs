using Avalonia.Controls;
using Avalonia.Input.Platform;

namespace GapMouse.App.Services;

public sealed class AvaloniaClipboardService(Func<TopLevel?> topLevel) : IClipboardService
{
    public async Task SetTextAsync(string text)
    {
        var clipboard = topLevel()?.Clipboard;
        if (clipboard is null)
        {
            return;
        }

        await clipboard.SetTextAsync(text);
    }
}
