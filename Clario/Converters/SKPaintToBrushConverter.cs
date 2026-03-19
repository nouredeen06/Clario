using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using LiveChartsCore.SkiaSharpView.Painting;

namespace Clario.Converters;

public class SKPaintToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is SolidColorPaint paint)
        {
            var color = Color.FromArgb(paint.Color.Alpha, paint.Color.Red, paint.Color.Green, paint.Color.Blue);
            return new SolidColorBrush(color);
        }

        return new SolidColorBrush(Colors.White);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}