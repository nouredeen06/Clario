using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace Clario.ViewModels;

public partial class BudgetViewModel : ViewModelBase
{
    public required ViewModelBase parentViewModel;

    [ObservableProperty] private ObservableCollection<PieData> _spendingBreakdown =
    [
        new() { Name = "Food & Dining", Values = [340d], Fill = new SolidColorPaint(SKColor.Parse("#2ECC8A")), InnerRadius = 60 },
        new() { Name = "Housing", Values = [540d], Fill = new SolidColorPaint(SKColor.Parse("#FF7E5E")), InnerRadius = 60 },
        new() { Name = "Transport", Values = [110d], Fill = new SolidColorPaint(SKColor.Parse("#7B9CFF")), InnerRadius = 60 },
        new() { Name = "Shopping", Values = [380d], Fill = new SolidColorPaint(SKColor.Parse("#FF5E5E")), InnerRadius = 60 },
        new() { Name = "Entertainment", Values = [170d], Fill = new SolidColorPaint(SKColor.Parse("#9B7BFF")), InnerRadius = 60 },
        new() { Name = "Health", Values = [69d], Fill = new SolidColorPaint(SKColor.Parse("#FF5E9B")), InnerRadius = 60 }
    ];
}

public partial class PieData : ObservableObject
{
    [ObservableProperty] private string _name;
    [ObservableProperty] private double[] _values;
    [ObservableProperty] private SolidColorPaint _fill;
    [ObservableProperty] private IBrush _bg;
    [ObservableProperty] private double _innerRadius = 60;
    [ObservableProperty] private Func<ChartPoint, string> _formatter;

    partial void OnFillChanged(SolidColorPaint value)
    {
        var color = Color.FromArgb(value.Color.Alpha, value.Color.Red, value.Color.Green, value.Color.Blue);
        Bg = new SolidColorBrush(color);
    }

    public PieData()
    {
        Formatter = point =>
        {
            var pct = point.StackedValue!.Share * 100;
            return $"${point.Coordinate.PrimaryValue:N0} ({pct:N1}%)";
        };
    }
}