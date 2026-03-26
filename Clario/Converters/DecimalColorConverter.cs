using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;

namespace Clario.Converters;

public class DecimalColorConverter : IMultiValueConverter
{
    public object? Convert(IList<object?>? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value.Count < 3 || value.Any(x => x is null)) return null;
        if (value[0] is decimal amount)
            return (amount < 0) ? value[1] : value[2];

        return null;
    }
}