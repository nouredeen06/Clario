using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Clario.Converters;

public class BoolToStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool b || parameter is not string s) return null;
        var results = s.Split('|');
        return b ? results[0] : results[1];
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}