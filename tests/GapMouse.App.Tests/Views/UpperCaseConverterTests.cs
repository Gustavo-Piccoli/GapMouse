using System.Globalization;
using GapMouse.App.Views;

namespace GapMouse.App.Tests.Views;

public sealed class UpperCaseConverterTests
{
    private static readonly UpperCaseConverter Upper = new();

    private static object? Convert(object? v, string culture = "en-US") =>
        Upper.Convert(v, typeof(string), null, CultureInfo.GetCultureInfo(culture));

    [Fact]
    public void Convert_UpperCasesAndHandlesNull()
    {
        Convert("movendo", "pt-BR").ShouldBe("MOVENDO");
        Convert("Pausado", "pt-BR").ShouldBe("PAUSADO");
        Convert("Paused").ShouldBe("PAUSED");
        Convert(null).ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData("tr-TR")]
    [InlineData("az-Latn-AZ")]
    public void Convert_IgnoresTurkishStyleCulture(string culture) =>
        Convert("Waiting", culture).ShouldBe("WAITING");
}
