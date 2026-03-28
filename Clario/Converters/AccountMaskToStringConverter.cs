using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Clario.Converters;

public class AccountMaskToStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string mask ||  string.IsNullOrWhiteSpace(mask)) return string.Empty;
        return $"•••• {mask}";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}