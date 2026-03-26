using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;

namespace Clario.Converters;

public class AmountSignConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Any(x => x is null) || values.Count < 2) return 0;
        if (values[0] is decimal amount && values[1] is string type)
            if (parameter is string param && param.Equals("round"))
                return (type.Equals("income", StringComparison.CurrentCultureIgnoreCase) ? $"${Math.Round(amount)}" : $"-${Math.Round(amount)}");
            else
                return (type.Equals("income", StringComparison.CurrentCultureIgnoreCase) ? $"${amount}" : $"-${amount}");

        return 0;
    }
}