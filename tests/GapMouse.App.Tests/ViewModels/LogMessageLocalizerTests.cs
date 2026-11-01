using GapMouse.App.Localization;
using GapMouse.App.ViewModels;
using GapMouse.Application.Logging;
using GapMouse.Application.Settings;
using GapMouse.Domain.Engine;

namespace GapMouse.App.Tests.ViewModels;

public sealed class LogMessageLocalizerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 30, 0, TimeSpan.Zero);

    private static string Format(EngineEvent e, UiLanguage language)
    {
        var l = new Localizer(() => new System.Globalization.CultureInfo("en-US"));
        l.SetLanguage(language);
        return LogMessageLocalizer.Format(LogEntryFormatter.FromEvent(e), l);
    }

    [Theory]
    [InlineData(false, UiLanguage.Portuguese, "Tela bloqueada; movimento parado, energia liberada")]
    [InlineData(true, UiLanguage.Portuguese, "Tela bloqueada; movimento parado, sistema mantido acordado")]
    [InlineData(false, UiLanguage.English, "Screen locked; movement stopped, power released")]
    [InlineData(true, UiLanguage.English, "Screen locked; movement stopped, system kept awake")]
    public void SessionLocked_IsLocalized(bool keepAwake, UiLanguage language, string expected) =>
        Format(new SessionLocked(Now, keepAwake), language).ShouldBe(expected);

    [Theory]
    [InlineData(UiLanguage.Portuguese, "Tela desbloqueada")]
    [InlineData(UiLanguage.English, "Screen unlocked")]
    public void SessionUnlocked_IsLocalized(UiLanguage language, string expected) =>
        Format(new SessionUnlocked(Now), language).ShouldBe(expected);
}
