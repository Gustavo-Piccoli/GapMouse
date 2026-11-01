using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using GapMouse.Application.Settings;

namespace GapMouse.App.Localization;

/// <summary>Live UI translation: bindings read the properties; changing the effective language refreshes them all.</summary>
public sealed partial class Localizer : ObservableObject
{
    private static readonly CultureInfo PortugueseCulture = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-US");

    private readonly Func<CultureInfo> _uiCulture;

    public Localizer(Func<CultureInfo>? uiCulture = null)
    {
        _uiCulture = uiCulture ?? (() => CultureInfo.CurrentUICulture);
        IsPortuguese = Resolve(UiLanguage.Auto);
    }

    public UiLanguage RequestedLanguage { get; private set; } = UiLanguage.Auto;

    public bool IsPortuguese { get; private set; }

    public CultureInfo Culture => IsPortuguese ? PortugueseCulture : EnglishCulture;

    public void SetLanguage(UiLanguage requested)
    {
        RequestedLanguage = requested;
        var portuguese = Resolve(requested);
        if (portuguese == IsPortuguese)
        {
            return;
        }

        IsPortuguese = portuguese;

        // An empty property name refreshes every binding on this object.
        OnPropertyChanged(string.Empty);
    }

    private string T(string en, string pt) => IsPortuguese ? pt : en;

    private bool Resolve(UiLanguage requested) => requested switch
    {
        UiLanguage.Portuguese => true,
        UiLanguage.English => false,
        _ => _uiCulture().TwoLetterISOLanguageName == "pt",
    };
}
