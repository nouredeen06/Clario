using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Clario.Converters;

public class SvgPathFromName : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string name) return null;
        return $"../Assets/Icons/{name}.svg";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}