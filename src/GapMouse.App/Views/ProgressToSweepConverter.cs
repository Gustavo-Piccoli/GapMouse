using System.Globalization;
using Avalonia.Data.Converters;

namespace GapMouse.App.Views;

/// <summary>Maps an idle progress fraction (0..1) to the sweep angle of the ring arc, in degrees.</summary>
public sealed class ProgressToSweepConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double progress && !double.IsNaN(progress) ? Math.Clamp(progress, 0.0, 1.0) * 360.0 : 0.0;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
