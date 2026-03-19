using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Clario.Converters;

public class TransactionTypeToAmountColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string type) return null;
        var app = Application.Current;
        if (app is null) return null;
        app.TryGetResource("AccentRed", app.ActualThemeVariant, out var resourceRed);
        app.TryGetResource("AccentGreen", app.ActualThemeVariant, out var resourceGreen);
        if (resourceRed is SolidColorBrush red && resourceGreen is SolidColorBrush green)
            return (type.Equals("income", StringComparison.CurrentCultureIgnoreCase) ? green : red);
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}