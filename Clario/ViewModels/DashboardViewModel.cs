using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Clario.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Clario.Models;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SKColor = SkiaSharp.SKColor;

namespace Clario.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    public required ViewModelBase parentViewModel;
    public required List<Transaction> Transactions = new();
    public required List<Category> Categories = new();
    public required List<Budget> Budgets = new();
    public required List<Account> Accounts = new();

    [ObservableProperty] private ObservableCollection<ColumnChartData> _spendingByCategoryChartData = new();
    [ObservableProperty] private ISeries[] _spendingByCategoryChartSeries = new ISeries[] { };
    [ObservableProperty] private ObservableCollection<Budget> _budgetsTrackerData = new();
    [ObservableProperty] private ObservableCollection<Account> _accountsSummaryData = new();
    [ObservableProperty] private ObservableCollection<Transaction> _recentTransactions = new();
    [ObservableProperty] private decimal _totalNetworth;
    [ObservableProperty] private decimal _monthlyIncome;
    private decimal _monthlyIncomeChange;

    public string MonthlyIncomeChangeFormatted => _monthlyIncomeChange >= 0
        ? "↑" + _monthlyIncomeChange.ToString("0.0%")
        : "↓" + _monthlyIncomeChange.ToString("0.0%");

    [ObservableProperty] private decimal _monthlyExpenses;
    private decimal _monthlyExpensesChange;

    public string MonthlyExpenseChangeFormatted => _monthlyExpensesChange >= 0
        ? "↑" + _monthlyExpensesChange.ToString("0.0%")
        : "↓" + _monthlyExpensesChange.ToString("0.0%");

    public string AccountsSubtitle =>
        AccountsSummaryData.Count == 1 ? $" {AccountsSummaryData.Count} linked Account" : $"{AccountsSummaryData.Count} linked Accounts";

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

        UpdateSpendingByCategoryChart(period);
    }

    public DashboardViewModel()
    {
    }

    public void initialize()
    {
        UpdateUserOverview();
    }

    [RelayCommand]
    private void UpdateUserOverview()
    {
        var thisMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        var lastMonth = thisMonth.AddMonths(-1);

        MonthlyIncome = Transactions.Where(x => x.Type == "income" && x.Date.Month == thisMonth.Month && x.Date.Year == thisMonth.Year)
            .Sum(x => x.Amount);
        MonthlyExpenses = Transactions.Where(x => x.Type == "expense" && x.Date.Month == DateTime.Now.Month && x.Date.Year == DateTime.Now.Year)
            .Sum(x => x.Amount);
        var lastMonthIncome = Transactions.Where(x => x.Type == "income" && x.Date.Month == lastMonth.Month && x.Date.Year == lastMonth.Year)
            .Sum(x => x.Amount);
        var lastMonthExpenses = Transactions.Where(x => x.Type == "expense" && x.Date.Month == lastMonth.Month && x.Date.Year == lastMonth.Year)
            .Sum(x => x.Amount);
        try
        {
            _monthlyIncomeChange = Math.Round((MonthlyIncome / ((lastMonthIncome == 0) ? 1 : lastMonthIncome)) - 1, 2);
            _monthlyExpensesChange = Math.Round((MonthlyExpenses / ((lastMonthExpenses == 0) ? 1 : lastMonthExpenses)) - 1, 2);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        OnPropertyChanged(nameof(MonthlyIncomeChangeFormatted));
        OnPropertyChanged(nameof(MonthlyExpenseChangeFormatted));

        UpdateSpendingByCategoryChart();
        _ = UpdateBudgetTracker();
        UpdateRecentTransactions();
        UpdateAccountsSummary();
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
        ((MainViewModel)parentViewModel).OpenAddTransaction();
    }

    private void UpdateSpendingByCategoryChart(ChartTimePeriod period = ChartTimePeriod.ThisMonth)
    {
        var tempList = new List<ColumnChartData>();

        foreach (var category in Categories)
        {
            var categoryTransactions =
                Transactions.Where(x => x.CategoryId == category.Id && x.Type.Equals("expense", StringComparison.OrdinalIgnoreCase));

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

    private async Task UpdateBudgetTracker()
    {
        var budgets = await DataRepo.General.FetchProcessedBudgets(new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1));
        BudgetsTrackerData = new ObservableCollection<Budget>(budgets.Where(x => !x.GroupHeader).OrderByDescending(x => x.PercentageUsed));
    }

    private void UpdateRecentTransactions()
    {
        RecentTransactions = new ObservableCollection<Transaction>(Transactions.OrderByDescending(x => x.Date).Take(5));
    }

    private void UpdateAccountsSummary()
    {
        foreach (var account in Accounts)
        {
            var accountTransactions = Transactions.Where(t => t.AccountId == account.Id).ToList();
            account.CurrentBalance = account.OpeningBalance + accountTransactions.Sum(t => t.Type == "income" ? t.Amount : -t.Amount);
            TotalNetworth += account.CurrentBalance;
        }

        AccountsSummaryData = new ObservableCollection<Account>(Accounts.OrderBy(x => x.CreatedAt));
        OnPropertyChanged(nameof(AccountsSubtitle));
    }

    private enum ChartTimePeriod
    {
        ThisMonth,
        LastMonth,
        ThisQuarter,
        ThisYear
    }
}