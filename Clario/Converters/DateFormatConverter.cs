using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Clario.Converters;

public class DateFormatConverter : IValueConverter
{
    private static readonly CultureInfo enUS = new("en-US");

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var format = parameter as string ?? "MMM dd, yyyy";
        return value switch
        {
            DateTime dt => dt.ToString(format, enUS),
            DateOnly date => date.ToString(format, enUS),
            _ => value
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}