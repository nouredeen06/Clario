using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Clario.Converters;

// BoolToColorConverter.cs
public class BoolToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool boolValue || parameter is not string colors)
            return AvaloniaProperty.UnsetValue;

        var parts = colors.Split('|');
        if (parts.Length != 2) return AvaloniaProperty.UnsetValue;

        var hex = boolValue ? parts[0] : parts[1];

        if (targetType == typeof(IBrush) || targetType == typeof(SolidColorBrush))
            return SolidColorBrush.Parse(hex);

        if (targetType == typeof(Color))
            return Color.Parse(hex);

        return SolidColorBrush.Parse(hex);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}