using System;
using System.Collections.ObjectModel;
using System.Security.Authentication;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Skia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Clario.Extensions;
using Clario.Services;
using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView.Painting;
using ShimSkiaSharp;
using SKColor = SkiaSharp.SKColor;

namespace Clario.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    public required ViewModelBase parentViewModel;
    [ObservableProperty] private ObservableCollection<ChartData> _chartData = new();

    public DashboardViewModel()
    {
        var app = Application.Current;
        if (app is null) return;
        
    }

    [RelayCommand]
    private void ViewAllTransactions()
    {
        if (parentViewModel is MainViewModel mainViewModel)
        {
            mainViewModel.GoToTransactionsCommand.Execute(null);
        }
    }
    
    [RelayCommand]
    private void CreateTransaction()
    {
        
    }
}

public partial class ChartData : ObservableObject
{
    [ObservableProperty] private string _name;
    [ObservableProperty] private double[] _values;
    [ObservableProperty] private SolidColorPaint _fill;

    public Func<ChartPoint, string> ToolTipFormatter => point => $"${point.Coordinate.PrimaryValue:N0}";
}