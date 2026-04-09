using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Clario.Data;
using Clario.Models;
using Clario.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using SKColor = SkiaSharp.SKColor;

namespace Clario.ViewModels;

public partial class AnalyticsViewModel : ViewModelBase
{
    public required ViewModelBase parentViewModel;
    public GeneralDataRepo AppData => DataRepo.General;

    private static readonly SKTypeface _interTypeface = SKTypeface.FromFamilyName("Inter");

    //  Period 
    public List<string> PeriodOptions { get; } = new()
    {
        "Last 30 Days", "Last 3 Months", "Last 6 Months", "Last 12 Months", "This Year"
    };

    [ObservableProperty] private string _selectedPeriod = "Last 6 Months";

    partial void OnSelectedPeriodChanged(string value) => Initialize();

    //  KPI cards 
    [ObservableProperty] private string _totalIncomeFormatted = "—";
    [ObservableProperty] private string _totalExpensesFormatted = "—";
    [ObservableProperty] private string _netSavingsFormatted = "—";
    [ObservableProperty] private string _savingsRateFormatted = "—";
    [ObservableProperty] private bool _netSavingsPositive = true;

    public string PrimarySymbol => CurrencyService.GetSymbol(AppData.PrimaryAccount?.Currency ?? AppData.Profile?.Currency ?? "USD");

    //  Cash Flow chart 
    [ObservableProperty] private ISeries[] _cashFlowSeries = [];
    [ObservableProperty] private Axis[] _cashFlowXAxes = [];
    [ObservableProperty] private Axis[] _cashFlowYAxes = [];

    //  Net Worth chart 
    [ObservableProperty] private ISeries[] _netWorthSeries = [];
    [ObservableProperty] private Axis[] _netWorthXAxes = [];
    [ObservableProperty] private Axis[] _netWorthYAxes = [];

    //  Day-of-week chart 
    [ObservableProperty] private ISeries[] _dayOfWeekSeries = [];
    [ObservableProperty] private Axis[] _dayOfWeekXAxes = [];

    //  Top categories 
    [ObservableProperty] private ObservableCollection<CategorySpendRow> _topCategories = new();
    [ObservableProperty] private bool _hasTopCategories;

    //  Income sources donut 
    [ObservableProperty] private ISeries[] _incomeSourcesSeries = [];
    [ObservableProperty] private bool _hasIncomeSources;

    //  State 
    [ObservableProperty] private bool _isExporting;
    [ObservableProperty] private string? _exportStatusMessage;

    // 

    public AnalyticsViewModel()
    {
        Track(AppData.Transactions, (_, _) => Initialize());
        Track(AppData.Accounts,     (_, _) => Initialize());
        Initialize();
    }

    public void Initialize()
    {
        try
        {
            var (start, end) = GetDateRange();
            var periodTxs = AppData.Transactions
                .Where(t => !t.IsTransfer && t.Date.Date >= start.Date && t.Date.Date <= end.Date)
                .ToList();

            var expenses = periodTxs.Where(t => t.Type == "expense").ToList();
            var income = periodTxs.Where(t => t.Type == "income").ToList();

            ComputeKpis(income, expenses);
            BuildCashFlowChart(start, end);
            BuildNetWorthChart(start, end);
            BuildDayOfWeekChart(expenses, start, end);
            BuildTopCategories(expenses);
            BuildIncomeSourcesChart(income);
            OnPropertyChanged(nameof(PrimarySymbol));
        }
        catch (Exception e)
        {
            DebugLogger.Log(e);
        }
    }

    //  Date range 

    private (DateTime start, DateTime end) GetDateRange()
    {
        var now = DateTime.Now;
        return SelectedPeriod switch
        {
            "Last 30 Days"   => (now.AddDays(-30), now),
            "Last 3 Months"  => (now.AddMonths(-3), now),
            "Last 6 Months"  => (now.AddMonths(-6), now),
            "Last 12 Months" => (now.AddMonths(-12), now),
            "This Year"      => (new DateTime(now.Year, 1, 1), now),
            _                => (now.AddMonths(-6), now)
        };
    }

    private static List<(DateTime monthStart, DateTime monthEnd, string label)> GetMonthBuckets(DateTime start, DateTime end)
    {
        var buckets = new List<(DateTime, DateTime, string)>();
        var current = new DateTime(start.Year, start.Month, 1);
        var endMonth = new DateTime(end.Year, end.Month, 1);
        var culture = new CultureInfo("en-US");
        while (current <= endMonth)
        {
            var next = current.AddMonths(1);
            buckets.Add((current, next.AddSeconds(-1), current.ToString("MMM ''yy", culture)));
            current = next;
        }
        return buckets;
    }

    //  Section 1: KPIs 

    private void ComputeKpis(List<Transaction> income, List<Transaction> expenses)
    {
        var sym = PrimarySymbol;
        var totalIncome = income.Sum(t => t.ConvertedAmount);
        var totalExpenses = expenses.Sum(t => t.ConvertedAmount);
        var net = totalIncome - totalExpenses;

        TotalIncomeFormatted = $"{sym}{totalIncome:N2}";
        TotalExpensesFormatted = $"{sym}{totalExpenses:N2}";
        NetSavingsPositive = net >= 0;
        NetSavingsFormatted = $"{(net >= 0 ? "+" : "")}{sym}{net:N2}";
        SavingsRateFormatted = totalIncome > 0
            ? $"{Math.Max(0, (net / totalIncome) * 100):F1}%"
            : "—";
    }

    //  Section 2: Cash Flow 

    private void BuildCashFlowChart(DateTime start, DateTime end)
    {
        var buckets = GetMonthBuckets(start, end);
        var incomeVals = new double[buckets.Count];
        var expenseVals = new double[buckets.Count];

        for (var i = 0; i < buckets.Count; i++)
        {
            var (mStart, mEnd, _) = buckets[i];
            incomeVals[i] = (double)AppData.Transactions
                .Where(t => t.Type == "income" && t.Date.Date >= mStart && t.Date.Date <= mEnd)
                .Sum(t => t.ConvertedAmount);
            expenseVals[i] = (double)AppData.Transactions
                .Where(t => t.Type == "expense" && t.Date.Date >= mStart && t.Date.Date <= mEnd)
                .Sum(t => t.ConvertedAmount);
        }

        var labels = buckets.Select(b => b.label).ToArray();

        CashFlowSeries =
        [
            new LineSeries<double>
            {
                Name = "Income",
                Values = incomeVals,
                Stroke = new SolidColorPaint(SKColor.Parse("#2ECC8A"), 2),
                Fill = null,
                GeometryFill = new SolidColorPaint(SKColor.Parse("#2ECC8A")),
                GeometryStroke = null,
                GeometrySize = 5,
                LineSmoothness = 0.5
            },
            new LineSeries<double>
            {
                Name = "Expenses",
                Values = expenseVals,
                Stroke = new SolidColorPaint(SKColor.Parse("#FF5E5E"), 2),
                Fill = null,
                GeometryFill = new SolidColorPaint(SKColor.Parse("#FF5E5E")),
                GeometryStroke = null,
                GeometrySize = 5,
                LineSmoothness = 0.5
            }
        ];

        CashFlowXAxes =
        [
            new Axis
            {
                Labels = labels,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#7A8090")) { SKTypeface = _interTypeface },
                SeparatorsPaint = new SolidColorPaint(new SKColor(30, 35, 48)),
                TicksPaint = null,
                TextSize = 11
            }
        ];

        var sym = PrimarySymbol;
        CashFlowYAxes =
        [
            new Axis
            {
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#7A8090")) { SKTypeface = _interTypeface },
                SeparatorsPaint = new SolidColorPaint(new SKColor(30, 35, 48)),
                TicksPaint = null,
                TextSize = 10,
                Labeler = v => $"{sym}{v:N0}"
            }
        ];
    }

    //  Section 3: Net Worth 

    private void BuildNetWorthChart(DateTime start, DateTime end)
    {
        // Start from 12 months before start to show history, but respect the selected range
        var buckets = GetMonthBuckets(start, end);
        var netWorthVals = new double[buckets.Count];

        for (var i = 0; i < buckets.Count; i++)
        {
            var (_, mEnd, _) = buckets[i];
            double nw = 0;
            foreach (var account in AppData.Accounts.Where(a => !a.IsArchived))
            {
                var txUpTo = AppData.Transactions.Where(t => t.AccountId == account.Id && t.Date.Date <= mEnd.Date);
                nw += (double)(account.OpeningBalance +
                               txUpTo.Sum(t => t.Type is "income" or "transfer_in" ? t.ConvertedAmount : -t.ConvertedAmount));
            }
            netWorthVals[i] = nw;
        }

        var labels = buckets.Select(b => b.label).ToArray();

        NetWorthSeries =
        [
            new LineSeries<double>
            {
                Name = "Net Worth",
                Values = netWorthVals,
                Stroke = new SolidColorPaint(SKColor.Parse("#7B9CFF"), 2),
                Fill = new SolidColorPaint(SKColor.Parse("#7B9CFF").WithAlpha(25)),
                GeometryFill = new SolidColorPaint(SKColor.Parse("#7B9CFF")),
                GeometryStroke = null,
                GeometrySize = 5,
                LineSmoothness = 0.5
            }
        ];

        NetWorthXAxes =
        [
            new Axis
            {
                Labels = labels,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#7A8090")) { SKTypeface = _interTypeface },
                SeparatorsPaint = new SolidColorPaint(new SKColor(30, 35, 48)),
                TicksPaint = null,
                TextSize = 11
            }
        ];

        var sym = PrimarySymbol;
        NetWorthYAxes =
        [
            new Axis
            {
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#7A8090")) { SKTypeface = _interTypeface },
                SeparatorsPaint = new SolidColorPaint(new SKColor(30, 35, 48)),
                TicksPaint = null,
                TextSize = 10,
                Labeler = v => $"{sym}{v:N0}"
            }
        ];
    }

    //  Section 4: Day of Week 

    private void BuildDayOfWeekChart(List<Transaction> expenses, DateTime start, DateTime end)
    {
        // DayOfWeek: Sunday=0, Monday=1 ... Saturday=6
        // We display Mon–Sun (index 0–6 in our array)
        var dayLabels = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
        var totals = new double[7];
        var counts = new int[7];

        // Count occurrences of each weekday in the period
        var d = start.Date;
        while (d <= end.Date)
        {
            var idx = ((int)d.DayOfWeek + 6) % 7; // Mon=0..Sun=6
            counts[idx]++;
            d = d.AddDays(1);
        }

        foreach (var tx in expenses)
        {
            var idx = ((int)tx.Date.DayOfWeek + 6) % 7;
            totals[idx] += (double)tx.ConvertedAmount;
        }

        var averages = totals.Select((total, i) => counts[i] > 0 ? total / counts[i] : 0).ToArray();

        DayOfWeekSeries =
        [
            new ColumnSeries<double>
            {
                Name = "Avg Daily Spend",
                Values = averages,
                Fill = new SolidColorPaint(SKColor.Parse("#9B7BFF")),
                MaxBarWidth = 40,
                Padding = 3
            }
        ];

        DayOfWeekXAxes =
        [
            new Axis
            {
                Labels = dayLabels,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#7A8090")) { SKTypeface = _interTypeface },
                SeparatorsPaint = null,
                TicksPaint = null,
                TextSize = 11
            }
        ];
    }

    //  Section 5: Top Categories 

    private void BuildTopCategories(List<Transaction> expenses)
    {
        var sym = PrimarySymbol;
        var totalSpend = expenses.Sum(t => t.ConvertedAmount);
        if (totalSpend == 0)
        {
            TopCategories = new ObservableCollection<CategorySpendRow>();
            HasTopCategories = false;
            return;
        }

        var grouped = expenses
            .Where(t => t.Category is not null)
            .GroupBy(t => t.Category!)
            .Select(g => new CategorySpendRow
            {
                Name = g.Key.Name,
                Icon = g.Key.Icon,
                Color = g.Key.Color,
                Amount = g.Sum(t => t.ConvertedAmount),
                Percentage = (double)(g.Sum(t => t.ConvertedAmount) / totalSpend * 100),
                AmountFormatted = $"{sym}{g.Sum(t => t.ConvertedAmount):N2}"
            })
            .OrderByDescending(r => r.Amount)
            .Take(8)
            .ToList();

        TopCategories = new ObservableCollection<CategorySpendRow>(grouped);
        HasTopCategories = grouped.Count > 0;
    }

    //  Section 6: Income Sources 

    private void BuildIncomeSourcesChart(List<Transaction> income)
    {
        var grouped = income
            .Where(t => t.Category is not null)
            .GroupBy(t => t.Category!)
            .Select(g => (category: g.Key, total: g.Sum(t => t.ConvertedAmount)))
            .OrderByDescending(x => x.total)
            .ToList();

        if (grouped.Count < 2)
        {
            IncomeSourcesSeries = [];
            HasIncomeSources = false;
            return;
        }

        var sym = PrimarySymbol;
        IncomeSourcesSeries = grouped.Select(x => (ISeries)new PieSeries<double>
        {
            Name = x.category.Name,
            Values = new[] { (double)x.total },
            Fill = new SolidColorPaint(SKColor.Parse(x.category.Color)),
            InnerRadius = 20,
            ToolTipLabelFormatter = p => $"{sym}{p.Coordinate.PrimaryValue:N2}"
        }).ToArray();

        HasIncomeSources = true;
    }

    //  PDF Export 

    [RelayCommand]
    private async Task ExportPdf()
    {
        if (IsExporting) return;
        IsExporting = true;
        ExportStatusMessage = null;
        try
        {
            var (start, end) = GetDateRange();
            var path = await PdfExportService.ExportAsync(
                AppData,
                start,
                end,
                SelectedPeriod,
                TopCategories.ToList());

            ExportStatusMessage = path is not null ? "PDF saved successfully." : null;
        }
        catch (Exception e)
        {
            DebugLogger.Log(e);
            ExportStatusMessage = "Export failed. Please try again.";
        }
        finally
        {
            IsExporting = false;
        }
    }
}
