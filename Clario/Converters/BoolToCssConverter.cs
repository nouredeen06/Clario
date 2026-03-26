using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace Clario.Converters;

// BoolToCssConverter.cs
public class BoolToCssConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool b || parameter is not string colors)
            return AvaloniaProperty.UnsetValue;

        var parts = colors.Split('|');
        if (parts.Length != 2) return AvaloniaProperty.UnsetValue;

        var hex = b ? parts[0] : parts[1];
        return $"path, circle, rect, ellipse, line, polyline, polygon, text, use {{ stroke: {hex}; }}";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}