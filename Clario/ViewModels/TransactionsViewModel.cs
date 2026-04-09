using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Clario.Data;
using Clario.Messages;
using Clario.Models;
using Clario.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace Clario.ViewModels;

public partial class TransactionsViewModel : ViewModelBase
{
    public required ViewModelBase parentViewModel;
    private GeneralDataRepo AppData => DataRepo.General;

    // ── Filter dropdowns ────────────────────────────────────────────────────

    [ObservableProperty] private ObservableCollection<Category> _categories = new();
    [ObservableProperty] private ObservableCollection<Account> _accounts = new();

    private static readonly IReadOnlyList<string> _sortOptions = new[]
    {
        "Date — Newest first", "Date — Oldest first",
        "Amount — High to low", "Amount — Low to high",
        "Category A → Z"
    };

    private static readonly IReadOnlyList<string> _dateRangeOptions = new[]
    {
        "All Time", "Today", "This Week", "This Month",
        "Last Month", "This Quarter", "This Year", "Custom Range"
    };

    public IReadOnlyList<string> SortOptions => _sortOptions;
    public IReadOnlyList<string> DateRangeOptions => _dateRangeOptions;

    // ── Active filter values ─────────────────────────────────────────────────

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private Category _selectedCategory;
    [ObservableProperty] private Account _selectedAccount;
    [ObservableProperty] private string _selectedSortOption = _sortOptions[0];
    [ObservableProperty] private string _selectedDateRangeOption = _dateRangeOptions[0];

    [ObservableProperty] private List<DateTime>? _selectedDates = new()
    {
        new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1),
        new DateTime(DateTime.Now.Year, DateTime.Now.Month,
            DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month))
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilterTypeAll), nameof(FilterTypeIncome),
                              nameof(FilterTypeExpense), nameof(FilterTypeTransfer))]
    private string _transactionType = "all";

    public bool FilterTypeAll     => TransactionType == "all";
    public bool FilterTypeIncome  => TransactionType == "income";
    public bool FilterTypeExpense => TransactionType == "expense";
    public bool FilterTypeTransfer => TransactionType == "transfer";

    // ── Filtered / paged data ────────────────────────────────────────────────

    private List<Transaction> _filteredAll = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredTransactionCount))]
    private List<Transaction> _filteredTransactions = new();

    public int FilteredTransactionCount => _filteredTransactions.Count;
    [ObservableProperty] private ObservableCollection<Transaction> _pagedTransactions = new();

    // ── Desktop pagination ───────────────────────────────────────────────────

    private int _pageSize = 25;
    [ObservableProperty] private int _pageSizeIndex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalPages))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand), nameof(PreviousPageCommand))]
    private int _currentPage = 1;

    [ObservableProperty] private string _paginationSummaryText = "";
    [ObservableProperty] private ObservableCollection<int> _visiblePageNumbers = new();

    public int TotalPages      => (int)Math.Ceiling(_filteredAll.Count / (double)_pageSize);
    public bool HasNoTransactions => _filteredAll.Count == 0;
    public bool HasPreviousPage   => CurrentPage > 1;

    // HasNextPage differs by platform
    public bool HasNextPage => App.IsMobile
        ? _mobileDisplayCount < _filteredAll.Count
        : CurrentPage < TotalPages;

    // ── Mobile infinite scroll ───────────────────────────────────────────────

    /// How many real (non-header) items are currently rendered in PagedTransactions.
    private int _mobileDisplayCount;

    // ── Summary stats ────────────────────────────────────────────────────────

    [ObservableProperty] private double _totalExpenses;
    [ObservableProperty] private double _totalIncome;
    [ObservableProperty] private int _expensesCount;
    [ObservableProperty] private int _incomeCount;
    [ObservableProperty] private string _dateRangeLabel = "";

    public string PrimaryCurrencySymbol =>
        CurrencyService.GetSymbol(AppData.PrimaryAccount?.Currency ?? AppData.Profile?.Currency ?? "USD");

    // ── Constructor ──────────────────────────────────────────────────────────

    public TransactionsViewModel()
    {
        Track(AppData.Transactions, (_, _) =>
        {
            InitializeCategories();
            InitializeAccounts();
            Refresh();
        });

        WeakReferenceMessenger.Default.Register<RatesRefreshed>(this, (_, _) => Refresh());

        Initialize();
    }

    // ── Initialization ───────────────────────────────────────────────────────

    public void Initialize()
    {
        try
        {
            InitializeCategories();
            InitializeAccounts();
            CalculateMonthlyFinancials();
            CurrentPage = 1;
            OnPropertyChanged(nameof(TotalPages));
            ResetFilters();
        }
        catch (Exception e)
        {
            DebugLogger.Log(e);
            throw;
        }
    }

    private void InitializeCategories()
    {
        Categories.Clear();
        Categories.Insert(0, new Category { Name = "All Categories" });
        foreach (var cat in AppData.Categories)
            Categories.Add(cat);
        SelectedCategory = Categories.First();
    }

    private void InitializeAccounts()
    {
        Accounts.Clear();
        Accounts.Insert(0, new Account { Name = "All Accounts" });
        foreach (var acc in AppData.Accounts)
            Accounts.Add(acc);
        SelectedAccount = Accounts.First();
    }

    private void CalculateMonthlyFinancials()
    {
        var now = DateTime.Now;
        var monthly = AppData.Transactions
            .Where(x => x.Date.Month == now.Month && x.Date.Year == now.Year);
        TotalExpenses = monthly.Where(x => x.Type == "expense").Sum(x => Convert.ToDouble(x.ConvertedAmount));
        TotalIncome   = monthly.Where(x => x.Type == "income").Sum(x => Convert.ToDouble(x.ConvertedAmount));
        ExpensesCount = monthly.Count(x => x.Type == "expense");
        IncomeCount   = monthly.Count(x => x.Type == "income");
    }

    // ── Filter pipeline ──────────────────────────────────────────────────────

    [RelayCommand]
    private void ApplyFilters()
    {
        var filteringByAccount = SelectedAccount?.Name != "All Accounts";

        // 1. Search + transfer-in visibility
        IEnumerable<Transaction> source = AppData.Transactions.Where(x =>
            (filteringByAccount || x.Type != "transfer_in") &&
            (x.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
             || (x.Note?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false)));

        // 2. Date range
        source = ApplyDateFilter(source, out var label);
        DateRangeLabel = label;

        // 3. Totals use the date-scoped set (before category/type filters)
        CalculateTotalsFromSource(source);

        // 4. Remaining filters
        source = ApplyCategoryFilter(source);
        source = ApplyAccountFilter(source);
        source = ApplyTypeFilter(source);
        source = ApplySortFilter(source);

        _filteredAll = source.ToList();
        FilteredTransactions = _filteredAll;
    }

    private IEnumerable<Transaction> ApplyDateFilter(IEnumerable<Transaction> source, out string label)
    {
        var (start, end, lbl) = DateRangeService.Resolve(SelectedDateRangeOption, SelectedDates);
        label = lbl;
        if (start is null || end is null) return source;
        return source.Where(x => x.Date.Date >= start.Value && x.Date.Date <= end.Value);
    }

    private void CalculateTotalsFromSource(IEnumerable<Transaction> source)
    {
        var list = source.ToList();
        TotalExpenses = list.Where(x => x.Type == "expense").Sum(x => Convert.ToDouble(x.ConvertedAmount));
        TotalIncome   = list.Where(x => x.Type == "income").Sum(x => Convert.ToDouble(x.ConvertedAmount));
    }

    private IEnumerable<Transaction> ApplyCategoryFilter(IEnumerable<Transaction> source)
    {
        if (SelectedCategory?.Name == "All Categories") return source;
        return source.Where(x => x.CategoryId == SelectedCategory?.Id);
    }

    private IEnumerable<Transaction> ApplyAccountFilter(IEnumerable<Transaction> source)
    {
        if (SelectedAccount?.Name == "All Accounts") return source;
        return source.Where(x => x.AccountId == SelectedAccount?.Id);
    }

    private IEnumerable<Transaction> ApplyTypeFilter(IEnumerable<Transaction> source) =>
        TransactionType switch
        {
            "income"   => source.Where(x => x.Type == "income"),
            "expense"  => source.Where(x => x.Type == "expense"),
            "transfer" => source.Where(x => x.IsTransfer),
            _          => source
        };

    private IEnumerable<Transaction> ApplySortFilter(IEnumerable<Transaction> source) =>
        SelectedSortOption switch
        {
            "Date — Oldest first"  => source.OrderBy(x => x.Date),
            "Amount — High to low" => source.OrderByDescending(x => x.Amount),
            "Amount — Low to high" => source.OrderBy(x => x.Amount),
            "Category A → Z"       => source.OrderBy(x => x.Category?.Name),
            _                      => source.OrderByDescending(x => x.Date) // default: newest first
        };

    // ── Desktop pagination ───────────────────────────────────────────────────

    partial void OnPageSizeIndexChanged(int value)
    {
        _pageSize = value switch { 1 => 50, 2 => 100, _ => 25 };
        LoadPage(1);
        OnPropertyChanged(nameof(HasNextPage));
        OnPropertyChanged(nameof(HasPreviousPage));
    }

    partial void OnCurrentPageChanged(int value)
    {
        if (App.IsMobile) return;
        LoadPage(value);
        OnPropertyChanged(nameof(HasNextPage));
        OnPropertyChanged(nameof(HasPreviousPage));
    }

    [RelayCommand]
    private void LoadPageStr(string page) => LoadPage(int.Parse(page));

    [RelayCommand]
    private void LoadPage(int page)
    {
        ApplyFilters();
        if (CurrentPage != page) CurrentPage = page;

        var items = _filteredAll.Skip((page - 1) * _pageSize).Take(_pageSize).ToList();

        PagedTransactions.Clear();
        foreach (var item in items) PagedTransactions.Add(item);

        OnPropertyChanged(nameof(HasNoTransactions));
        OnPropertyChanged(nameof(HasNextPage));
        OnPropertyChanged(nameof(HasPreviousPage));

        PaginationSummaryText = _filteredAll.Count == 0
            ? "No transactions"
            : $"Showing {(page - 1) * _pageSize + 1}–{Math.Min(page * _pageSize, _filteredAll.Count)} of {_filteredAll.Count}";

        var allPages = Enumerable.Range(1, Math.Max(TotalPages, 1)).ToList();
        VisiblePageNumbers.Clear();
        foreach (var n in GetSurrounding(allPages, page)) VisiblePageNumbers.Add(n);

        WeakReferenceMessenger.Default.Send(new TransactionsScrollToTop());
        GroupTransactions();
    }

    [RelayCommand(CanExecute = nameof(HasNextPage))]
    private void NextPage() { if (CurrentPage < TotalPages) CurrentPage++; }

    [RelayCommand(CanExecute = nameof(HasPreviousPage))]
    private void PreviousPage() { if (CurrentPage > 1) CurrentPage--; }

    // ── Mobile infinite scroll ───────────────────────────────────────────────

    private void RefreshMobile()
    {
        _mobileDisplayCount = 0;
        PagedTransactions.Clear();
        AppendMobileItems(_pageSize * 3);
        OnPropertyChanged(nameof(HasNoTransactions));
        OnPropertyChanged(nameof(HasNextPage));
    }

    private void AppendMobileItems(int count)
    {
        var batch = _filteredAll.Skip(_mobileDisplayCount).Take(count).ToList();

        foreach (var item in batch)
        {
            var needsHeader = _mobileDisplayCount == 0
                || item.Date.Date != _filteredAll[_mobileDisplayCount - 1].Date.Date;

            if (needsHeader)
            {
                PagedTransactions.Add(new Transaction
                {
                    Description = DateRangeService.FormatGroupHeader(item.Date),
                    Date = item.Date,
                    GroupHeader = true
                });
            }

            PagedTransactions.Add(item);
            _mobileDisplayCount++;
        }

        OnPropertyChanged(nameof(HasNextPage));
    }

    /// Adds 3 pages of items at once. Shown behind "Load More" button.
    [RelayCommand(CanExecute = nameof(HasNextPage))]
    private void LoadMore()
    {
        if (_mobileDisplayCount >= _filteredAll.Count) return;
        AppendMobileItems(_pageSize * 3);
    }

    // ── Shared helpers ───────────────────────────────────────────────────────

    private void Refresh()
    {
        CalculateMonthlyFinancials();
        if (App.IsMobile) { ApplyFilters(); RefreshMobile(); }
        else LoadPage(CurrentPage);
    }

    [RelayCommand]
    private void ResetFilters()
    {
        SearchText = "";
        SelectedCategory = Categories.FirstOrDefault() ?? new Category { Name = "All Categories" };
        SelectedAccount = Accounts.FirstOrDefault() ?? new Account { Name = "All Accounts" };
        TransactionType = "all";
        SelectedSortOption = SortOptions[0];
        SelectedDateRangeOption = DateRangeOptions[0];

        if (App.IsMobile) { ApplyFilters(); RefreshMobile(); }
        else LoadPage(1);
    }

    [RelayCommand]
    private void SetTransactionType(string type) => TransactionType = type;

    /// Desktop: inserts date group headers into PagedTransactions.
    private void GroupTransactions()
    {
        // Remove all existing headers
        foreach (var h in PagedTransactions.Where(x => x.GroupHeader).ToList())
            PagedTransactions.Remove(h);

        // Insert a header before the first item of each date group
        var dates = PagedTransactions.Select(x => x.Date.Date).Distinct().ToList();
        foreach (var date in dates)
        {
            var firstItem = PagedTransactions.FirstOrDefault(x => !x.GroupHeader && x.Date.Date == date);
            if (firstItem is null) continue;
            PagedTransactions.Insert(PagedTransactions.IndexOf(firstItem), new Transaction
            {
                Description = DateRangeService.FormatGroupHeader(date),
                Date = date,
                GroupHeader = true
            });
        }
    }

    private static List<T> GetSurrounding<T>(List<T> list, T item, int count = 5)
    {
        var index = list.IndexOf(item);
        if (index == -1) return new List<T>();
        var start = Math.Max(0, Math.Min(index - count / 2, list.Count - count));
        return list.GetRange(start, Math.Min(count, list.Count - start));
    }

    // ── Navigation ───────────────────────────────────────────────────────────

    [RelayCommand]
    private void CreateTransaction()
    {
        if (parentViewModel is MainViewModel main) main.OpenAddTransaction();
    }

    [RelayCommand]
    private void EditTransaction(Transaction transaction)
    {
        if (parentViewModel is MainViewModel main) main.OpenEditTransaction(transaction);
    }
}
