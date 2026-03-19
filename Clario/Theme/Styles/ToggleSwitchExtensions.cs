using Avalonia;
using Avalonia.Controls;

namespace Clario.Theme.Styles
{
    public static class ToggleSwitchExtensions
    {
        public static readonly AttachedProperty<double> KnobWidthProperty =
            AvaloniaProperty.RegisterAttached<ToggleSwitch, double>(
                "KnobWidth",
                typeof(ToggleSwitchExtensions),
                16.0); // default value

        public static readonly AttachedProperty<double> KnobHeightProperty =
            AvaloniaProperty.RegisterAttached<ToggleSwitch, double>(
                "KnobHeight",
                typeof(ToggleSwitchExtensions),
                16.0); // default value

        public static readonly AttachedProperty<double> CanvasWidthProperty =
            AvaloniaProperty.RegisterAttached<ToggleSwitch, double>(
                "CanvasWidth",
                typeof(ToggleSwitchExtensions),
                24.0); // default: 10 + 12

        public static readonly AttachedProperty<double> CanvasHeightProperty =
            AvaloniaProperty.RegisterAttached<ToggleSwitch, double>(
                "CanvasHeight",
                typeof(ToggleSwitchExtensions),
                24.0); // default: 10 + 12


        static ToggleSwitchExtensions()
        {
            // Update CanvasWidth when KnobWidth changes
            KnobWidthProperty.Changed.AddClassHandler<ToggleSwitch>((toggle, e) =>
            {
                if (e.NewValue is double width)
                {
                    // Use SetValue internally
                    toggle.SetValue(CanvasWidthProperty, width + 8);
                }
            });

            // Update CanvasHeight when KnobHeight changes
            KnobHeightProperty.Changed.AddClassHandler<ToggleSwitch>((toggle, e) =>
            {
                if (e.NewValue is double height)
                {
                    // Use SetValue internally
                    toggle.SetValue(CanvasHeightProperty, height + 8);
                }
            });
        }

        public static void SetKnobWidth(AvaloniaObject element, double value) =>
            element.SetValue(KnobWidthProperty, value);

        public static double GetKnobWidth(AvaloniaObject element) =>
            element.GetValue(KnobWidthProperty);

        public static void SetKnobHeight(AvaloniaObject element, double value) =>
            element.SetValue(KnobHeightProperty, value);

        public static double GetKnobHeight(AvaloniaObject element) =>
            element.GetValue(KnobHeightProperty);

        public static double GetCanvasWidth(AvaloniaObject element) =>
            element.GetValue(CanvasWidthProperty);

        public static double GetCanvasHeight(AvaloniaObject element) =>
            element.GetValue(CanvasHeightProperty);
    }
}