using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Styling;

namespace Clario.Extensions;

public static class ScrollViewerExtensions
{
    public static async Task SmoothScrollToEnd(this ScrollViewer scrollViewer, double durationMs = 300)
    {
        double endY = scrollViewer.Extent.Height - scrollViewer.Viewport.Height;

        endY = Math.Max(0, endY);

        var endPoint = new Vector(scrollViewer.Offset.X, endY);
        var animation = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(durationMs),
            Easing = new CubicEaseInOut(),
            Children =
            {
                new KeyFrame
                {
                    KeyTime = TimeSpan.FromMilliseconds(durationMs),
                    Setters =
                    {
                        new Setter(ScrollViewer.OffsetProperty, endPoint)
                    }
                }
            }
        };

        await animation.RunAsync(scrollViewer);
    }

    public static async Task SmoothScrollToHome(this ScrollViewer scrollViewer, double durationMs = 300)
    {
        var endPoint = new Vector(scrollViewer.Offset.X, 0);
        var animation = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(durationMs),
            Easing = new CubicEaseInOut(),
            Children =
            {
                new KeyFrame
                {
                    KeyTime = TimeSpan.FromMilliseconds(durationMs),
                    Setters =
                    {
                        new Setter(ScrollViewer.OffsetProperty, endPoint)
                    }
                }
            }
        };

        await animation.RunAsync(scrollViewer);
    }
}