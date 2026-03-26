using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Clario.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
        chart.AddHandler(PointerWheelChangedEvent, OnChartScroll, RoutingStrategies.Tunnel);
    }

    private void OnChartScroll(object? sender, PointerWheelEventArgs e)
    {
        var offset = mainScrollviewer.Offset;
        mainScrollviewer.Offset = new Vector(
            offset.X,
            offset.Y - e.Delta.Y * mainScrollviewer.SmallChange.Height * 3
        );

        e.Handled = true;
    }
}