using Avalonia.Styling;
using GapMouse.App.Localization;
using GapMouse.Application.Settings;

namespace GapMouse.App.Services;

/// <summary>Applies the theme and language from the settings now and whenever they change.</summary>
public sealed class ThemeApplier(Localizer localizer, ISettingsService settings)
{
    private bool _started;

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        Apply(settings.Current);
        settings.Changed += (_, current) => Apply(current);
    }

    private void Apply(AppSettings current)
    {
        if (Avalonia.Application.Current is { } app)
        {
            app.RequestedThemeVariant = current.Theme == AppTheme.Light ? ThemeVariant.Light : ThemeVariant.Dark;
        }

        localizer.SetLanguage(current.Language);
    }
}
