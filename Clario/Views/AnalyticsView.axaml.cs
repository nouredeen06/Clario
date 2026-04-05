using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Clario.Views;

public partial class AnalyticsView : UserControl
{
    public AnalyticsView()
    {
        InitializeComponent();
        this.AddHandler(PointerWheelChangedEvent, WindowScrollHandler, RoutingStrategies.Tunnel);
    }

    private void WindowScrollHandler(object? sender, PointerWheelEventArgs e)
    {
        var offset = mainScrollviewer.Offset;
        mainScrollviewer.Offset = new Vector(
            offset.X,
            offset.Y - e.Delta.Y * mainScrollviewer.SmallChange.Height * 3
        );

        e.Handled = true;
    }
}