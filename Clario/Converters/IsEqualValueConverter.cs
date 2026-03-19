using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;
using Clario.Models;

namespace Clario.Converters;

public class IsEqualValueConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2 || values.Any(x => x is null)) return false;
        if (values.All(x => x is Account)) return ((Account)values[0]).Id == ((Account)values[1]).Id;
        return values[0].Equals(values[1]);
    }
}