using System;
using System.Globalization;
using Avalonia.Controls.Converters;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Clario.Converters;

public class HexToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string hex || parameter is not string type) return null;
        if (type == "css")
            return $"path, circle, rect, ellipse, line, polyline, polygon, text, use {{ stroke: {hex}; }}";
        if (type == "brush")
            return new SolidColorBrush(Color.Parse(hex));
        if (type == "color")
            return Color.Parse(hex);
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is not string type) return null;
        var color = Color.Parse("#ffffff");
        if (value is Color c)
        {
            color = c;
        }

        if (value is SolidColorBrush b)
        {
            color = b.Color;
        }

        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}