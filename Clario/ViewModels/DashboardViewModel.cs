using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Clario.Data;
using Clario.Messages;
using Clario.Services;
using CommunityToolkit.Mvvm.Messaging;
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
    public GeneralDataRepo AppData => DataRepo.General;
    // public required List<Account> Accounts = new();

    [ObservableProperty] private ObservableCollection<ColumnChartData> _spendingByCategoryChartData = new();
    [ObservableProperty] private ISeries[] _spendingByCategoryChartSeries = new ISeries[] { };
    [ObservableProperty] private ObservableCollection<Budget> _budgetsTrackerData = new();
    [ObservableProperty] private ObservableCollection<Account> _accountsSummaryData = new();
    [ObservableProperty] private ObservableCollection<Transaction> _recentTransactions = new();
    [ObservableProperty] private decimal _totalNetworth;
    public string PrimarySymbol => CurrencyService.GetSymbol(AppData.PrimaryAccount?.Currency ?? AppData.Profile?.Currency ?? "USD");
    [ObservableProperty] private decimal _monthlyIncome;
    private decimal _monthlyIncomeChange;
    private bool _hasLastMonthIncome;

    public int MaxChartWidth => SpendingByCategoryChartData.Count * 150;

    public string MonthlyIncomeChangeFormatted
    {
        get
        {
            if (!_hasLastMonthIncome)
                return MonthlyIncome > 0 ? "NEW" : "—";
            return _monthlyIncomeChange >= 0
                ? "↑ " + _monthlyIncomeChange.ToString("0.0%")
                : "↓ " + _monthlyIncomeChange.ToString("0.0%");
        }
    }

    [ObservableProperty] private decimal _monthlyExpenses;
    private decimal _monthlyExpensesChange;
    private bool _hasLastMonthExpenses;

    public string MonthlyExpenseChangeFormatted
    {
        get
        {
            if (!_hasLastMonthExpenses)
                return MonthlyExpenses > 0 ? "NEW" : "—";
            return _monthlyExpensesChange >= 0
                ? "↑ " + _monthlyExpensesChange.ToString("0.0%")
                : "↓ " + _monthlyExpensesChange.ToString("0.0%");
        }
    }

    public string AccountsSubtitle =>
        AccountsSummaryData.Count == 1 ? $" {AccountsSummaryData.Count} linked Account" : $"{AccountsSummaryData.Count} linked Accounts";

    public bool HasSpendingData => SpendingByCategoryChartData.Any();
    public bool HasBudgetData => BudgetsTrackerData.Any();
    public bool HasTransactionData => RecentTransactions.Any();

    [ObservableProperty] private List<string> _chartTimePeriods = new()
    {
        "This Month",
        "Last Month",
        "This Quarter",
        "This Year"
    };

    [ObservableProperty] private string _selectedChartTimePeriod = "This Month";
    [ObservableProperty] private string _selectedChartTimPeriodSubTitle = DateTime.Now.ToString("MMMM yyyy");
    [ObservableProperty] private string _dateToday = DateTime.Now.ToString("dddd, MMMM d, yyyy");

    partial void OnSelectedChartTimePeriodChanged(string value)
    {
        var (_, _, subtitle) = DateRangeService.Resolve(value);
        SelectedChartTimPeriodSubTitle = subtitle.Length > 0
            ? char.ToUpper(subtitle[0]) + subtitle.Substring(1).ToLower()
            : subtitle;

        UpdateSpendingByCategoryChart(value);
    }

    public DashboardViewModel()
    {
        Track(AppData.Transactions, (_, _) => UpdateUserOverview());
        Track(AppData.Accounts,     (_, _) => UpdateUserOverview());
        Track(AppData.Categories,   (_, _) => UpdateUserOverview());
        Track(AppData.Budgets,      (_, _) => UpdateUserOverview());
        WeakReferenceMessenger.Default.Register<RatesRefreshed>(this, (_, _) => UpdateUserOverview());
        Initialize();
    }

    public void Initialize()
    {
        UpdateUserOverview();
    }

    [RelayCommand]
    private void UpdateUserOverview()
    {
        CalculateMonthlyValues();
        UpdateSpendingByCategoryChart(SelectedChartTimePeriod);
        _ = UpdateBudgetTracker();
        UpdateRecentTransactions();
        UpdateAccountsSummary();
    }

    private void CalculateMonthlyValues()
    {
        var thisMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        var lastMonth = thisMonth.AddMonths(-1);

        MonthlyIncome = AppData.Transactions.Where(x => x.Type == "income" && x.Date.Month == thisMonth.Month && x.Date.Year == thisMonth.Year)
            .Sum(x => x.ConvertedAmount);
        MonthlyExpenses = AppData.Transactions.Where(x => x.Type == "expense" && x.Date.Month == DateTime.Now.Month && x.Date.Year == DateTime.Now.Year)
            .Sum(x => x.ConvertedAmount);
        var lastMonthIncome = AppData.Transactions.Where(x => x.Type == "income" && x.Date.Month == lastMonth.Month && x.Date.Year == lastMonth.Year)
            .Sum(x => x.ConvertedAmount);
        var lastMonthExpenses = AppData.Transactions.Where(x => x.Type == "expense" && x.Date.Month == lastMonth.Month && x.Date.Year == lastMonth.Year)
            .Sum(x => x.ConvertedAmount);

        _hasLastMonthIncome = lastMonthIncome > 0;
        _hasLastMonthExpenses = lastMonthExpenses > 0;

        if (_hasLastMonthIncome)
        {
            _monthlyIncomeChange = Math.Round((MonthlyIncome / lastMonthIncome) - 1, 2);
        }

        if (_hasLastMonthExpenses)
        {
            _monthlyExpensesChange = Math.Round((MonthlyExpenses / lastMonthExpenses) - 1, 2);
        }

        OnPropertyChanged(nameof(MonthlyIncomeChangeFormatted));
        OnPropertyChanged(nameof(MonthlyExpenseChangeFormatted));
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
        if (parentViewModel is MainViewModel main) main.OpenAddTransaction();
    }

    [RelayCommand]
    private void NavigateToSettings()
    {
        if (parentViewModel is MainViewModel main) main.GoToSettingsCommand.Execute(null);
    }

    [RelayCommand]
    private void NavigateToBudget()
    {
        if (parentViewModel is MainViewModel main) main.GoToBudgetCommand.Execute(null);
    }

    [RelayCommand]
    private void OpenAddBudget()
    {
        if (parentViewModel is MainViewModel main) main.OpenAddBudgetCommand.Execute(null);
    }

    private void UpdateSpendingByCategoryChart(string period = "This Month")
    {
        var (start, end, _) = DateRangeService.Resolve(period);
        var tempList = new List<ColumnChartData>();

        foreach (var category in AppData.Categories)
        {
            var txns = AppData.Transactions
                .Where(x => x.CategoryId == category.Id
                         && x.Type.Equals("expense", StringComparison.OrdinalIgnoreCase)
                         && (start is null || x.Date.Date >= start.Value)
                         && (end is null   || x.Date.Date <= end.Value));

            var total = txns.Sum(x => x.ConvertedAmount);
            if (total == 0) continue;

            tempList.Add(new ColumnChartData
            {
                id = category.Id,
                Name = category.Name,
                Values = [(double)total],
                Fill = new SolidColorPaint(SKColor.Parse(category.Color))
            });
        }

        tempList = tempList.OrderByDescending(x => x.Values[0]).ToList();
        SpendingByCategoryChartData = new ObservableCollection<ColumnChartData>(tempList);
        SpendingByCategoryChartSeries = tempList.Select(x => (ISeries)new ColumnSeries<double>
        {
            Name = x.Name,
            Values = x.Values,
            Fill = x.Fill,
            Padding = 4,
            MaxBarWidth = 150
        }).ToArray();
        OnPropertyChanged(nameof(HasSpendingData));
        OnPropertyChanged(nameof(MaxChartWidth));
    }

    private async Task UpdateBudgetTracker()
    {
        var budgets = await DataRepo.General.FetchProcessedBudgets(new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1));
        BudgetsTrackerData = new ObservableCollection<Budget>(budgets.Where(x => !x.GroupHeader).OrderByDescending(x => x.PercentageUsed));
        OnPropertyChanged(nameof(HasBudgetData));
    }

    private void UpdateRecentTransactions()
    {
        RecentTransactions = new ObservableCollection<Transaction>(AppData.Transactions.Where(x => !x.IsTransfer).OrderByDescending(x => x.Date).Take(5));
        OnPropertyChanged(nameof(HasTransactionData));
    }

    private void UpdateAccountsSummary()
    {
        TotalNetworth = 0;
        var primaryCurrency = AppData.PrimaryAccount?.Currency ?? AppData.Profile?.Currency ?? "USD";
        foreach (var account in AppData.Accounts.Where(a => !a.IsArchived))
        {
            var accountTransactions = AppData.Transactions.Where(t => t.AccountId == account.Id).ToList();
            account.CurrentBalance = account.OpeningBalance + accountTransactions.Sum(t => t.Type is "income" or "transfer_in" ? t.Amount : -t.Amount);
            if (account.Currency.Equals(primaryCurrency, StringComparison.OrdinalIgnoreCase))
                TotalNetworth += account.CurrentBalance;
            else
                TotalNetworth += accountTransactions.Sum(t => t.Type is "income" or "transfer_in" ? t.ConvertedAmount : -t.ConvertedAmount);
        }

        AccountsSummaryData = new ObservableCollection<Account>(AppData.Accounts.Where(a => !a.IsArchived).OrderBy(x => x.CreatedAt));
        OnPropertyChanged(nameof(AccountsSubtitle));
    }
}