using System;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView.Painting;
using Newtonsoft.Json;

namespace Clario.Models;

public partial class ColumnChartData : ObservableObject
{
    public Guid id;
    [ObservableProperty] private string _name;
    [ObservableProperty] private double[] _values;
    [ObservableProperty] private SolidColorPaint _fill;

    
    [JsonIgnore] public Func<ChartPoint, string> ToolTipFormatter => point => $"${point.Coordinate.PrimaryValue:N0}";
}