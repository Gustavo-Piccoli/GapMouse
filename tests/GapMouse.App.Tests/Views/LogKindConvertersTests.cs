using System.Globalization;
using Avalonia.Media;
using GapMouse.App.Views;
using GapMouse.Application.Logging;

namespace GapMouse.App.Tests.Views;

public sealed class LogKindConvertersTests
{
    private static readonly LogKindBrushConverter Fill = new();
    private static readonly LogKindInkConverter Ink = new();

    private static Color Convert(Avalonia.Data.Converters.IValueConverter converter, object? value) =>
        ((ISolidColorBrush)converter.Convert(value, typeof(IBrush), null, CultureInfo.InvariantCulture)!).Color;

    [Theory]
    [InlineData(LogKind.Start, "#475569", "#FFFFFF")]
    [InlineData(LogKind.Waiting, "#2F5FD0", "#FFFFFF")]
    [InlineData(LogKind.Moving, "#157545", "#FFFFFF")]
    [InlineData(LogKind.Return, "#5B6474", "#FFFFFF")]
    [InlineData(LogKind.Paused, "#E3A21A", "#1F1503")]
    [InlineData(LogKind.Resumed, "#E3A21A", "#1F1503")]
    [InlineData(LogKind.Config, "#6B5BD6", "#FFFFFF")]
    [InlineData(LogKind.Stop, "#B42318", "#FFFFFF")]
    [InlineData(LogKind.Error, "#B42318", "#FFFFFF")]
    [InlineData(LogKind.Warn, "#C2410C", "#FFFFFF")]
    public void Convert_ReturnsTagFillAndInk(LogKind kind, string fill, string ink)
    {
        Convert(Fill, kind).ShouldBe(Color.Parse(fill));
        Convert(Ink, kind).ShouldBe(Color.Parse(ink));
    }

    [Fact]
    public void Convert_NonLogKind_ReturnsReturnPair()
    {
        Convert(Fill, "x").ShouldBe(Color.Parse("#5B6474"));
        Convert(Ink, null).ShouldBe(Color.Parse("#FFFFFF"));
    }

    [Fact]
    public void ConvertBack_IsNotSupported()
    {
        Should.Throw<NotSupportedException>(() => Fill.ConvertBack(null, typeof(object), null, CultureInfo.InvariantCulture));
        Should.Throw<NotSupportedException>(() => Ink.ConvertBack(null, typeof(object), null, CultureInfo.InvariantCulture));
    }
}
