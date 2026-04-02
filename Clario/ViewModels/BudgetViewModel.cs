using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Clario.Data;
using Clario.Messages;
using Clario.Models;
using Clario.Services;
using CommunityToolkit.Mvvm.Messaging;
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
    public GeneralDataRepo AppData => DataRepo.General;

    [ObservableProperty] private ObservableCollection<Budget> _visibleBudgets = new();

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(NextPeriodCommand), nameof(PreviousPeriodCommand))]
    private DateTime _currentPeriod = DateTime.Now.Date;

    public bool CanGoToNextPeriod => CurrentPeriod.Month < DateTime.Now.Month;
    public bool CanGoToPreviousPeriod => AppData.Transactions.Any() && CurrentPeriod.Month > AppData.Transactions.Min(x => x.Date.Month);
    public string CurrentPeriodFormatted => CurrentPeriod.ToString("MMMM yyyy");

    [ObservableProperty] private ISeries[] _spendingBreakdownChartSeries = [];
    [ObservableProperty] private List<Budget> _spendingBreakdownLegends = [];

    [ObservableProperty] private decimal _totalSpent;
    [ObservableProperty] private decimal _totalBudgeted;
    public string SpentPercentageFormatted => (TotalSpent / TotalBudgeted).ToString("P0") + " of total budget.";

    public decimal TotalLeft => Math.Clamp(Math.Round(TotalBudgeted - TotalSpent), 0, decimal.MaxValue);
    private string PrimarySymbol => CurrencyService.GetSymbol(AppData.PrimaryAccount?.Currency ?? AppData.Profile?.Currency ?? "USD");
    public string TotalLeftFormatted => $"{PrimarySymbol}{TotalLeft:N0} left";

    public bool HasSavingsGoal => AppData.Profile?.SavingsGoal is > 0;

    public bool IsSavingsGoalMet => HasSavingsGoal && TotalLeft >= (AppData.Profile!.SavingsGoal ?? 0);

    public string SavingsHint => IsSavingsGoalMet
        ? "You're on track to meet your savings goal this month!"
        : $"Reduce your spending by {PrimarySymbol}{((AppData.Profile?.SavingsGoal ?? 0) - TotalLeft):N0} to hit your goal.";

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

    public string DailyBudgetLeftFormatted =>
        $"{PrimarySymbol}{((TotalBudgeted - TotalSpent) / ((PeriodDaysLeft == 0) ? 1 : PeriodDaysLeft)):N2}";

    public BudgetViewModel()
    {
        AppData.Budgets.CollectionChanged += async (_, _) => { await Initialize(); };
        AppData.Transactions.CollectionChanged += async (_, _) => { await Initialize(); };
        AppData.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppData.Profile))
                NotifyComputedPropertiesOnChanged();
        };
        WeakReferenceMessenger.Default.Register<RatesRefreshed>(this, async (_, _) => await Initialize());
        _ = Initialize();
    }

    private async Task Initialize()
    {
        try
        {
            await ProcessBudgets();
            ProcessChartData();
            
        }
        catch (Exception e)
        {
            DebugLogger.Log(e);
            throw;
        }
    }

    [RelayCommand]
    private void CreateBudget()
    {
        ((MainViewModel)parentViewModel).OpenAddBudgetCommand.Execute(null);
    }

    [RelayCommand]
    private void EditBudget(Budget budget)
    {
        ((MainViewModel)parentViewModel).OpenEditBudgetCommand.Execute(budget);
    }

    [RelayCommand]
    private void EditSavingsGoal()
    {
        ((MainViewModel)parentViewModel).OpenEditSavingsGoalCommand.Execute(null);
    }

    private void ProcessChartData()
    {
        var tempCategorySpendingBreakdown = new List<(Category category, double[] spent)>();
        var tempSpendingBreakdownLegends = new List<Budget>();
        foreach (var category in AppData.Categories)
        {
            var spent = AppData.Transactions
                .Where(x => x.CategoryId == category.Id && x.Type.Equals("expense", StringComparison.OrdinalIgnoreCase) &&
                            x.Date.Month == CurrentPeriod.Month && x.Date.Year == CurrentPeriod.Year)
                .Sum(x => x.ConvertedAmount);
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
            ToolTipLabelFormatter = point => $"{PrimarySymbol}{point.Coordinate.PrimaryValue:N0}"
        }).ToArray();

        SpendingBreakdownLegends = tempSpendingBreakdownLegends.OrderByDescending(x => x.Spent).ToList();
    }

    private async Task ProcessBudgets()
    {
        VisibleBudgets.Clear();
        VisibleBudgets = new ObservableCollection<Budget>(await DataRepo.General.FetchProcessedBudgets(CurrentPeriod));
        _onTrackCount = VisibleBudgets.Count(x => x is { IsOnTrack: true, GroupHeader: false });
        _approachingCount = VisibleBudgets.Count(x => x is { IsWarning: true, GroupHeader: false });
        _overBudgetCount = VisibleBudgets.Count(x => x is { IsOverBudget: true, GroupHeader: false });
        TotalBudgeted = VisibleBudgets.Sum(x => x.LimitAmount);
        TotalSpent = VisibleBudgets.Sum(x => x.Spent);

        NotifyComputedPropertiesOnChanged();
    }

    private void NotifyComputedPropertiesOnChanged()
    {
        OnPropertyChanged(nameof(CanGoToNextPeriod));
        OnPropertyChanged(nameof(CanGoToPreviousPeriod));
        OnPropertyChanged(nameof(CurrentPeriodFormatted));
        OnPropertyChanged(nameof(SpentPercentageFormatted));
        OnPropertyChanged(nameof(TotalLeft));
        OnPropertyChanged(nameof(TotalLeftFormatted));
        OnPropertyChanged(nameof(HasSavingsGoal));
        OnPropertyChanged(nameof(IsSavingsGoalMet));
        OnPropertyChanged(nameof(SavingsHint));
        OnPropertyChanged(nameof(OnTrackCountFormatted));
        OnPropertyChanged(nameof(ApproachingCountFormatted));
        OnPropertyChanged(nameof(OverBudgetCountFormatted));
        OnPropertyChanged(nameof(PeriodLength));
        OnPropertyChanged(nameof(PeriodDaysPassed));
        OnPropertyChanged(nameof(PeriodDaysLeftFormatted));
        OnPropertyChanged(nameof(DailyBudgetLeftFormatted));
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