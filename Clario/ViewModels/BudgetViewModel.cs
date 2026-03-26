using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Clario.Data;
using Clario.Models;
using Clario.Models.GeneralModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView.Avalonia;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace Clario.ViewModels;

public partial class BudgetViewModel : ViewModelBase
{
    public required ViewModelBase parentViewModel;
    [ObservableProperty] private Profile? _profile;
    public required List<Budget> Budgets = new();
    [ObservableProperty] private ObservableCollection<Budget> _visibleBudgets = new();
    public required List<Category> Categories = new();
    public required List<Transaction> Transactions = new();

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(NextPeriodCommand), nameof(PreviousPeriodCommand))]
    private DateTime _currentPeriod = DateTime.Now.Date;

    public bool CanGoToNextPeriod => CurrentPeriod.Month < DateTime.Now.Month;
    public bool CanGoToPreviousPeriod => CurrentPeriod.Month > Transactions.Min(x => x.Date.Month);
    public string CurrentPeriodFormatted => CurrentPeriod.ToString("MMMM yyyy");

    [ObservableProperty] private ISeries[] _spendingBreakdownChartSeries = [];
    [ObservableProperty] private List<Budget> _spendingBreakdownLegends = [];

    [ObservableProperty] private decimal _totalSpent;
    [ObservableProperty] private decimal _totalBudgeted;
    public string SpentPercentageFormatted => (TotalSpent / TotalBudgeted).ToString("P0") + " of total budget.";

    public decimal TotalLeft => Math.Clamp(Math.Round(TotalBudgeted - TotalSpent), 0, decimal.MaxValue);
    public string TotalLeftFormatted => TotalLeft.ToString("C0") + " left";

    public string SavingsHint => TotalLeft >= (Profile != null ? Profile.SavingsGoal : 0)
        ? "You're on track!"
        : $"Reduce your spending by ${Math.Round((Profile != null ? Profile.SavingsGoal ?? 0 : 0) - TotalLeft)} to hit your goal.";

    private int _onTrackCount;
    private int _approachingCount;
    private int _overBudgetCount;

    public string OnTrackCountFormatted => _onTrackCount == 1 ? _onTrackCount + " Budget" : _onTrackCount + " Budgets";
    public string ApproachingCountFormatted => _approachingCount == 1 ? _approachingCount + " Budget" : _approachingCount + " Budgets";
    public string OverBudgetCountFormatted => _overBudgetCount == 1 ? _overBudgetCount + " Budget" : _overBudgetCount + " Budgets";

    public int PeriodLength => DateTime.DaysInMonth(CurrentPeriod.Year, CurrentPeriod.Month);
    public int PeriodDaysPassed => DateTime.Now.Day;
    private int PeriodDaysLeft => PeriodLength - PeriodDaysPassed;
    public string PeriodDaysLeftFormatted => PeriodDaysLeft == 1 ? PeriodDaysLeft + " day left" : PeriodDaysLeft + " days left";

    public string DailyBudgetLeftFormatted => ((TotalBudgeted - TotalSpent) / PeriodDaysLeft).ToString("C", new CultureInfo("en-US"));

    public BudgetViewModel()
    {
    }

    public async Task Initialize()
    {
        try
        {
            await ProcessBudgets();
            ProcessChartData();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private void ProcessChartData()
    {
        var categories = Categories;
        var transactions = Transactions;
        var tempCategorySpendingBreakdown = new List<(Category category, double[] spent)>();
        var tempSpendingBreakdownLegends = new List<Budget>();
        foreach (var category in categories)
        {
            var spent = transactions
                .Where(x => x.CategoryId == category.Id && x.Type.Equals("expense", StringComparison.OrdinalIgnoreCase) &&
                            x.Date.Month == CurrentPeriod.Month && x.Date.Year == CurrentPeriod.Year)
                .Sum(x => x.Amount);
            if (spent == 0) continue;
            double[] values = [(double)spent];
            tempCategorySpendingBreakdown.Add((category, values));
            tempSpendingBreakdownLegends.Add(new Budget() { Category = category, Spent = spent });
        }


        SpendingBreakdownChartSeries = tempCategorySpendingBreakdown.OrderByDescending(x => x.spent.Sum()).Select(x => (ISeries)new XamlPieSeries()
        {
            Name = x.category.Name,
            Values = x.spent,
            Fill = new SolidColorPaint(SKColor.Parse(x.category.Color)),
            InnerRadius = 60,
            ToolTipLabelFormatter = point => $"${point.Coordinate.PrimaryValue:N0}"
        }).ToArray();

        SpendingBreakdownLegends = tempSpendingBreakdownLegends.OrderByDescending(x => x.Spent).ToList();
    }

    private async Task ProcessBudgets()
    {
        VisibleBudgets.Clear();
        VisibleBudgets = new ObservableCollection<Budget>(await DataRepo.General.FetchProcessedBudgets(CurrentPeriod));
        _onTrackCount = VisibleBudgets.Count(x => x.IsOnTrack);
        _approachingCount = VisibleBudgets.Count(x => x.IsWarning);
        _overBudgetCount = VisibleBudgets.Count(x => x.IsOverBudget);
        TotalBudgeted = VisibleBudgets.Sum(x => x.LimitAmount);
        TotalSpent = VisibleBudgets.Sum(x => x.Spent);
    }

    [RelayCommand(CanExecute = nameof(CanGoToNextPeriod))]
    private async Task NextPeriod()
    {
        CurrentPeriod = CurrentPeriod.AddMonths(1);
        OnPropertyChanged(nameof(CurrentPeriodFormatted));
        ProcessChartData();
        await ProcessBudgets();
    }

    [RelayCommand(CanExecute = nameof(CanGoToPreviousPeriod))]
    private async Task PreviousPeriod()
    {
        CurrentPeriod = CurrentPeriod.AddMonths(-1);
        OnPropertyChanged(nameof(CurrentPeriodFormatted));
        ProcessChartData();
        await ProcessBudgets();
    }
}