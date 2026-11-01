using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using GapMouse.Application.Logging;

namespace GapMouse.App.Views;

/// <summary>Maps a <see cref="LogKind"/> to the fill of its stamped log tag (identical in both themes).</summary>
public sealed class LogKindBrushConverter : IValueConverter
{
    private static readonly IBrush Slate = Solid("#475569");
    private static readonly IBrush Blue = Solid("#2F5FD0");
    private static readonly IBrush Green = Solid("#157545");
    private static readonly IBrush Gray = Solid("#5B6474");
    private static readonly IBrush Amber = Solid("#E3A21A");
    private static readonly IBrush Violet = Solid("#6B5BD6");
    private static readonly IBrush Red = Solid("#B42318");
    private static readonly IBrush Orange = Solid("#C2410C");

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is LogKind kind
        ? kind switch
        {
            LogKind.Start => Slate,
            LogKind.Waiting => Blue,
            LogKind.Moving => Green,
            LogKind.Return => Gray,
            LogKind.Paused or LogKind.Resumed or LogKind.Locked or LogKind.Unlocked => Amber,
            LogKind.Config => Violet,
            LogKind.Stop or LogKind.Error => Red,
            LogKind.Warn => Orange,
            _ => Gray,
        }
        : Gray;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static ImmutableSolidColorBrush Solid(string hex) => new ImmutableSolidColorBrush(Color.Parse(hex));
}
