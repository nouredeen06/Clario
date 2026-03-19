using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Authentication;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Skia;
using Avalonia.Styling;
using Clario.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Clario.Extensions;
using Clario.Models;
using Clario.Services;
using ExCSS;
using LiveChartsCore;
using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using ShimSkiaSharp;
using SKColor = SkiaSharp.SKColor;

namespace Clario.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    public required ViewModelBase parentViewModel;
    [ObservableProperty] private ObservableCollection<ColumnChartData> _spendingByCategoryChartData = new();
    [ObservableProperty] private ISeries[] _spendingByCategoryChartSeries = new ISeries[] { };

    [ObservableProperty] private List<string> _chartTimePeriods = new()
    {
        "This Month",
        "Last Month",
        "This Quarter",
        "This Year"
    };

    [ObservableProperty] private string _selectedChartTimePeriod = "This Month";

    partial void OnSelectedChartTimePeriodChanged(string value)
    {
        ChartTimePeriod period = value switch
        {
            "This Month" => ChartTimePeriod.ThisMonth,
            "Last Month" => ChartTimePeriod.LastMonth,
            "This Quarter" => ChartTimePeriod.ThisQuarter,
            "This Year" => ChartTimePeriod.ThisYear,
            _ => ChartTimePeriod.ThisMonth
        };

        _ = UpdateSpendingByCategoryChart(period);
    }

    public DashboardViewModel()
    {
        var app = Application.Current;
        if (app is null) return;
        _ = UpdateSpendingByCategoryChart();
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

    private async Task UpdateSpendingByCategoryChart(ChartTimePeriod period = ChartTimePeriod.ThisMonth)
    {
        var transactions = await DataRepo.General.FetchTransactions();
        var categories = await DataRepo.General.FetchCategories();
        var tempList = new List<ColumnChartData>();

        foreach (var category in categories)
        {
            var categoryTransactions =
                transactions.Where(x => x.CategoryId == category.Id && x.Type.Equals("expense", StringComparison.OrdinalIgnoreCase));

            switch (period)
            {
                case ChartTimePeriod.ThisMonth:
                    categoryTransactions = categoryTransactions.Where(x => x.Date.Month == DateTime.Now.Month);
                    break;

                case ChartTimePeriod.LastMonth:
                    categoryTransactions = categoryTransactions.Where(x => x.Date.Month == DateTime.Now.AddMonths(-1).Month);
                    break;

                case ChartTimePeriod.ThisQuarter:
                    categoryTransactions = categoryTransactions.Where(x =>
                        x.Date.Month >= DateTime.Now.AddMonths(-(DateTime.Now.Month - 1) % 3).Month &&
                        x.Date.Month <= DateTime.Now.AddMonths(-(DateTime.Now.Month - 1) % 3).AddMonths(3).Month);
                    break;

                case ChartTimePeriod.ThisYear:
                    categoryTransactions = categoryTransactions.Where(x => x.Date.Year == DateTime.Now.Year);
                    break;

                default:
                    categoryTransactions = categoryTransactions.Where(x => x.Date.Month == DateTime.Now.Month);
                    break;
            }

            var balance = categoryTransactions.Sum(x => x.Amount);
            if (balance == 0) continue;
            tempList.Add(new ColumnChartData()
                { id = category.Id, Name = category.Name, Values = [(double)balance], Fill = new SolidColorPaint(SKColor.Parse(category.Color)) });
        }

        tempList = tempList.OrderByDescending(x => x.Values[0]).ToList();
        SpendingByCategoryChartData = new ObservableCollection<ColumnChartData>(tempList);
        SpendingByCategoryChartSeries = tempList.Select(x => (ISeries)new ColumnSeries<double>
        {
            Name = x.Name,
            Values = x.Values,
            Fill = x.Fill,
            Padding = 4,
            MaxBarWidth = double.MaxValue
        }).ToArray();
    }

    private enum ChartTimePeriod
    {
        ThisMonth,
        LastMonth,
        ThisQuarter,
        ThisYear
    }
}