using Avalonia;
using Avalonia.Media;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace Clario.Extensions;

public static class ColorExtensions
{
    public static SolidColorPaint ToSKPaint(this string resourceKey)
    {
        if (Application.Current!.TryGetResource(resourceKey, Application.Current.ActualThemeVariant, out var resource) == true &&
            resource is SolidColorBrush brush)
        {
            var c = brush.Color;
            return new SolidColorPaint(new SKColor(c.R, c.G, c.B, c.A));
        }

        return new SolidColorPaint(SKColors.White);
    }
}