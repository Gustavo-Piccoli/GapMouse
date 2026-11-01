using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using GapMouse.Application.Logging;

namespace GapMouse.App.Views;

/// <summary>Maps a <see cref="LogKind"/> to a brush readable on both themes.</summary>
public sealed class LogKindBrushConverter : IValueConverter
{
    private static readonly IBrush Yellow = new SolidColorBrush(Color.Parse("#CA8A04"));
    private static readonly IBrush Cyan = new SolidColorBrush(Color.Parse("#0891B2"));
    private static readonly IBrush Green = new SolidColorBrush(Color.Parse("#16A34A"));
    private static readonly IBrush Gray = new SolidColorBrush(Color.Parse("#6B7280"));
    private static readonly IBrush Magenta = new SolidColorBrush(Color.Parse("#C026D3"));
    private static readonly IBrush DimGray = new SolidColorBrush(Color.Parse("#9CA3AF"));
    private static readonly IBrush Red = new SolidColorBrush(Color.Parse("#DC2626"));
    private static readonly IBrush Amber = new SolidColorBrush(Color.Parse("#D97706"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is LogKind kind
        ? kind switch
        {
            LogKind.Start => Yellow,
            LogKind.Waiting => Cyan,
            LogKind.Moving => Green,
            LogKind.Return => Gray,
            LogKind.Paused or LogKind.Resumed => Magenta,
            LogKind.Config => DimGray,
            LogKind.Stop => Red,
            LogKind.Warn => Amber,
            LogKind.Error => Red,
            _ => Gray,
        }
        : Gray;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
