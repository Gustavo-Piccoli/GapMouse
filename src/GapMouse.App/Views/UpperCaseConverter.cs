using System.Globalization;
using Avalonia.Data.Converters;

namespace GapMouse.App.Views;

/// <summary>Upper-cases text for display, culture-independent (no Turkish dotted I from the OS culture).</summary>
public sealed class UpperCaseConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (value as string)?.ToUpperInvariant() ?? string.Empty;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
