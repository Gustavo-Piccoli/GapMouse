using System.Globalization;
using GapMouse.App.Localization;
using GapMouse.Application.Settings;

namespace GapMouse.App.Tests.Localization;

public sealed class LocalizerTests
{
    private static Localizer Create(string uiCulture = "en-US") =>
        new(() => CultureInfo.GetCultureInfo(uiCulture));

    [Fact]
    public void SetLanguage_Portuguese_ReturnsPortugueseText()
    {
        var localizer = Create();

        localizer.SetLanguage(UiLanguage.Portuguese);

        localizer.TabSettings.ShouldBe("Configurações");
        localizer.TabHome.ShouldBe("Início");
    }

    [Fact]
    public void SetLanguage_English_ReturnsEnglishText()
    {
        var localizer = Create("pt-BR");

        localizer.SetLanguage(UiLanguage.English);

        localizer.TabSettings.ShouldBe("Settings");
        localizer.IsPortuguese.ShouldBeFalse();
    }

    [Fact]
    public void SetLanguage_AutoWithPtBrUiCulture_IsPortuguese()
    {
        var localizer = Create("pt-BR");

        localizer.SetLanguage(UiLanguage.Auto);

        localizer.IsPortuguese.ShouldBeTrue();
        localizer.RequestedLanguage.ShouldBe(UiLanguage.Auto);
    }

    [Fact]
    public void SetLanguage_AutoWithFrUiCulture_IsEnglish()
    {
        var localizer = Create("fr-FR");

        localizer.SetLanguage(UiLanguage.Auto);

        localizer.IsPortuguese.ShouldBeFalse();
    }

    [Fact]
    public void SetLanguage_Changed_RaisesPropertyChangedWithEmptyName()
    {
        var localizer = Create();
        var names = new List<string?>();
        localizer.PropertyChanged += (_, e) => names.Add(e.PropertyName);

        localizer.SetLanguage(UiLanguage.Portuguese);

        names.ShouldBe([string.Empty]);
    }

    [Fact]
    public void SetLanguage_Same_DoesNotRaise()
    {
        var localizer = Create();
        var raised = 0;
        localizer.PropertyChanged += (_, _) => raised++;

        localizer.SetLanguage(UiLanguage.English);
        localizer.SetLanguage(UiLanguage.Auto);

        raised.ShouldBe(0);
    }

    [Fact]
    public void Culture_Portuguese_IsPtBr()
    {
        var localizer = Create();

        localizer.SetLanguage(UiLanguage.Portuguese);

        localizer.Culture.Name.ShouldBe("pt-BR");
    }

    [Fact]
    public void Culture_English_IsEnUs()
    {
        var localizer = Create("pt-BR");

        localizer.SetLanguage(UiLanguage.English);

        localizer.Culture.Name.ShouldBe("en-US");
    }

    [Fact]
    public void HomeRecentTitle_IsLocalized()
    {
        var localizer = Create();

        localizer.SetLanguage(UiLanguage.English);
        localizer.HomeRecentTitle.ShouldBe("Recent");

        localizer.SetLanguage(UiLanguage.Portuguese);
        localizer.HomeRecentTitle.ShouldBe("Recentes");
    }

    [Fact]
    public void LogsEmpty_IsLocalized()
    {
        var localizer = Create();

        localizer.SetLanguage(UiLanguage.English);
        localizer.LogsEmpty.ShouldBe("No activity yet. Every start, movement, return and pause will appear here.");

        localizer.SetLanguage(UiLanguage.Portuguese);
        localizer.LogsEmpty.ShouldBe("Nenhuma atividade ainda. Cada início, movimento, retorno e pausa aparece aqui.");
    }
}
