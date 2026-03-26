using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;

namespace Clario.Converters;

public class PercentageConverter : IMultiValueConverter
{
    public object? Convert(IList<object?>? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value.Count < 2 || value.Any(x => x is null)) return "0%";

        if (value[0] is decimal part && value[1] is decimal total && part > 0)
        {
            var percentage = Math.Round(part / total, 3);
            return percentage.ToString("0.0%");
        }

        return "0%";
    }
}