using System.Globalization;
using GapMouse.App.Views;

namespace GapMouse.App.Tests.Views;

public sealed class ProgressToSweepConverterTests
{
    private static readonly ProgressToSweepConverter Sweep = new();
    private static readonly UpperCaseConverter Upper = new();

    private static object? Convert(Avalonia.Data.Converters.IValueConverter c, object? v, string culture = "en-US") =>
        c.Convert(v, typeof(double), null, CultureInfo.GetCultureInfo(culture));

    [Theory]
    [InlineData(0.375, 135.0)]
    [InlineData(1.5, 360.0)]
    [InlineData(-1.0, 0.0)]
    [InlineData(double.NaN, 0.0)]
    public void Convert_ScalesAndClampsToDegrees(double progress, double expected) =>
        Convert(Sweep, progress).ShouldBe(expected);

    [Fact]
    public void Convert_NonDouble_ReturnsZero() => Convert(Sweep, "x").ShouldBe(0.0);

    [Fact]
    public void UpperCase_UsesCultureAndHandlesNull()
    {
        Convert(Upper, "movendo", "pt-BR").ShouldBe("MOVENDO");
        Convert(Upper, "Paused").ShouldBe("PAUSED");
        Convert(Upper, null).ShouldBe(string.Empty);
    }
}
