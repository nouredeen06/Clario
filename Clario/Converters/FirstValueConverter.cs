using System;
using System.Globalization;
using System.Runtime.InteropServices.JavaScript;
using Avalonia.Data.Converters;

namespace Clario.Converters;

public class FirstValueConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double[])
        {
            return ((double[])value)[0];
        }

        return 0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}