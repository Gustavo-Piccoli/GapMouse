using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using GapMouse.Application.Logging;

namespace GapMouse.App.Views;

/// <summary>Maps a <see cref="LogKind"/> to the ink drawn on its tag fill (see <see cref="LogKindBrushConverter"/>).</summary>
public sealed class LogKindInkConverter : IValueConverter
{
    private static readonly IBrush White = new ImmutableSolidColorBrush(Color.Parse("#FFFFFF"));
    private static readonly IBrush DarkAmber = new ImmutableSolidColorBrush(Color.Parse("#1F1503"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is LogKind.Paused or LogKind.Resumed ? DarkAmber : White;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
