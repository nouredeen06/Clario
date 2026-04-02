using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;

namespace Clario.Converters;

public class NetworthSumConverter : IMultiValueConverter
{
    public object? Convert(IList<object?>? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || value.Count < 2) return 0;
        var symbol = value.Count >= 3 && value[2] is string s ? s : "$";

        if (value[0] is double incomeD && value[1] is double expenseD)
        {
            var net = incomeD - expenseD;
            return net < 0 ? $"- {symbol}{Math.Abs(net):F2}" : $"+ {symbol}{Math.Abs(net):F2}";
        }

        if (value[0] is decimal incomeDec && value[1] is decimal expenseDec)
        {
            var net = incomeDec - expenseDec;
            return net < 0 ? $"-{symbol}{Math.Abs(net):F2}" : $"+{symbol}{Math.Abs(net):F2}";
        }

        return 0;
    }
}