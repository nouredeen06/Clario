using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Clario.Converters;

public class DecimalSignConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal d)
            return (d < 0 ? $"-${Math.Abs(Math.Round(d))}" : $"+${Math.Abs(Math.Round(d))}");
        return "$0";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}