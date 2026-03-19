using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Clario.Data;
using Clario.Messages;
using Clario.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace Clario.ViewModels;

public partial class TransactionsViewModel : ViewModelBase
{
    public required ViewModelBase parentViewModel;

    private List<Transaction> _allTransactions = new();
    private List<Transaction> _filteredTransactions = new();

    private int _pageSize = 25;
    [ObservableProperty] private int _pageSizeIndex = 0;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(TotalPages))] [NotifyCanExecuteChangedFor(nameof(NextPageCommand), nameof(PreviousPageCommand))]
    private int _currentPage = 1;

    [ObservableProperty] private string _paginationSummaryText;

    [ObservableProperty] private ObservableCollection<Transaction> _pagedTransactions = new();
    [ObservableProperty] private ObservableCollection<Category> _categories = new();
    [ObservableProperty] private ObservableCollection<Account> _accounts = new();

    [ObservableProperty] private ObservableCollection<string> _sortOptions = new()
    {
        "Date — Newest first",
        "Date — Oldest first",
        "Amount — High to low",
        "Amount — Low to high",
        "Category A → Z"
    };

    [ObservableProperty] private ObservableCollection<string> _DateRangeOptions = new()
    {
        "All Time",
        "Today",
        "This Week",
        "This Month",
        "Last Month",
        "This Quarter",
        "This Year",
        "Custom Range"
    };

    public List<int> PageNumbers { get; set; }
    [ObservableProperty] private ObservableCollection<int> _visiblePageNumbers = new();
    public int TotalPages => (int)Math.Ceiling(_filteredTransactions.Count / (double)_pageSize);
    public bool HasNoTransactions => _filteredTransactions.Count == 0;
    public bool HasNextPage => CurrentPage < TotalPages;
    public bool HasPreviousPage => CurrentPage > 1;

    [ObservableProperty] private double _totalExpenses;
    [ObservableProperty] private double _totalIncome;
    [ObservableProperty] private int _expensesCount;
    [ObservableProperty] private int _incomeCount;

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private Category _selectedCategory;
    [ObservableProperty] private Account _selectedAccount;
    [ObservableProperty] private string _selectedSortOption;
    [ObservableProperty] private string _selectedDateRangeOption;

    [ObservableProperty] private List<DateTime>? _selectedDates = new()
    {
        new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1),
        new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month))
    };

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(FilterTypeAll), nameof(FilterTypeIncome), nameof(FilterTypeExpense))]
    private string _transactionType = "all";


    public TransactionsViewModel()
    {
        _ = Initialize();
    }

    partial void OnPageSizeIndexChanged(int value)
    {
        _pageSize = value switch
        {
            0 => 25,
            1 => 50,
            2 => 100,
            _ => 25
        };


        LoadPage(1);
        OnPropertyChanged(nameof(HasNextPage));
        OnPropertyChanged(nameof(HasPreviousPage));
    }


    partial void OnCurrentPageChanged(int value)
    {
        LoadPage(value);
        OnPropertyChanged(nameof(HasNextPage));
        OnPropertyChanged(nameof(HasPreviousPage));
    }

    [RelayCommand]
    private void LoadPageStr(string page)
    {
        LoadPage(int.Parse(page));
    }

    [RelayCommand]
    private void LoadPage(int page)
    {
        ApplyFilters();
        if (CurrentPage != page) CurrentPage = page;
        var items = _filteredTransactions
            .Skip((page - 1) * _pageSize)
            .Take(_pageSize);

        OnPropertyChanged(nameof(HasNoTransactions));
        PagedTransactions.Clear();
        foreach (var item in items)
            PagedTransactions.Add(item);
        PaginationSummaryText =
            $"Showing {((page - 1) * _pageSize) + 1}-{(Math.Min(page * _pageSize, _filteredTransactions.Count))} of {_filteredTransactions.Count} transactions";
        PageNumbers = Enumerable.Range(1, Math.Min(TotalPages, 5)).ToList();
        var numbers = GetSurrounding(PageNumbers, page, 5);
        VisiblePageNumbers.Clear();
        foreach (var number in numbers)
            VisiblePageNumbers.Add(number);
        WeakReferenceMessenger.Default.Send(new TransactionsScrollToTop());
        GroupTransactions();
    }

    private void ApplyFilters()
    {
        // Console.WriteLine($"Search Text: {_searchText}");
        // Console.WriteLine($"Category: {_selectedCategory.Name}");
        // Console.WriteLine($"Account: {_selectedAccount.Name}");
        // Console.WriteLine($"Transaction Type: {_transactionType}");


        var filtered = _allTransactions.Where(x =>
            x.Description.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
            || x.Note.Contains(_searchText, StringComparison.OrdinalIgnoreCase));

        switch (SelectedDateRangeOption)
        {
            case "All Time":
                // do nothing
                break;
            case "Today":
                filtered = filtered.Where(x => x.Date == DateTime.Now.Date);
                break;
            case "This Week":
                var startOfWeek = DateTime.Now.Date.AddDays(-(int)DateTime.Now.DayOfWeek);
                var endOfWeek = startOfWeek.AddDays(6);
                filtered = filtered.Where(x => x.Date.Date >= startOfWeek && x.Date.Date <= endOfWeek);
                break;
            case "This Month":
                filtered = filtered.Where(x => x.Date.Month == DateTime.Now.Month);
                break;
            case "Last Month":
                var lastMonth = DateTime.Now.AddMonths(-1);
                filtered = filtered.Where(x => x.Date.Month == lastMonth.Month && x.Date.Year == lastMonth.Year);
                break;
            case "This Quarter":
                var startOfQuarter = DateTime.Now.AddMonths(-(DateTime.Now.Month - 1) % 3);
                var endOfQuarter = startOfQuarter.AddMonths(3);
                filtered = filtered.Where(x => x.Date >= startOfQuarter && x.Date <= endOfQuarter);
                break;
            case "This Year":
                filtered = filtered.Where(x => x.Date.Year == DateTime.Now.Year);
                break;
            case "Custom Range":
                if (SelectedDates is not null && SelectedDates.Count > 0)
                {
                    var ordered = SelectedDates
                        .Select(d => d.Date)
                        .Distinct()
                        .OrderBy(d => d)
                        .ToList();

                    var start = ordered.First();
                    var end = ordered.Last();

                    Console.WriteLine($"first {SelectedDates.First():d} / last {SelectedDates.Last():d}");
                    if (SelectedDates.Count == 1)
                        filtered = filtered.Where(x => x.Date.Date == start);
                    else
                        filtered = filtered.Where(x => x.Date.Date >= start && x.Date.Date <= end);
                }

                break;
        }

        if (_selectedCategory.Name != "All Categories")
            filtered = filtered.Where(x => x.CategoryId == _selectedCategory.Id);

        if (_selectedAccount.Name != "All Accounts")
            filtered = filtered.Where(x => x.AccountId == _selectedAccount.Id);

        if (_transactionType != "all")
            filtered = filtered.Where(x => x.Type == _transactionType);

        switch (SelectedSortOption)
        {
            case "Date — Newest first":
                filtered = filtered.OrderByDescending(x => x.Date);
                break;
            case "Date — Oldest first":
                filtered = filtered.OrderBy(x => x.Date);
                break;
            case "Amount — High to low":
                filtered = filtered.OrderByDescending(x => x.Amount);
                break;
            case "Amount — Low to high":
                filtered = filtered.OrderBy(x => x.Amount);
                break;
            case "Category A → Z":
                filtered = filtered.OrderBy(x => x.Category?.Name);
                break;
        }


        _filteredTransactions = filtered.ToList();
    }

    [RelayCommand]
    private void ResetFilters()
    {
        SearchText = "";
        SelectedCategory = Categories.First();
        SelectedAccount = Accounts.First();
        TransactionType = "all";
        SelectedSortOption = SortOptions.First();
        SelectedDateRangeOption = DateRangeOptions.First();
        LoadPage(1);
    }

    [RelayCommand]
    private void SetTransactionType(string type)
    {
        TransactionType = type;
    }

    public bool FilterTypeAll => TransactionType == "all";
    public bool FilterTypeIncome => TransactionType == "income";
    public bool FilterTypeExpense => TransactionType == "expense";

    [RelayCommand(CanExecute = nameof(HasNextPage))]
    private void NextPage()
    {
        if (CurrentPage < TotalPages) CurrentPage++;
    }

    [RelayCommand(CanExecute = nameof(HasPreviousPage))]
    private void PreviousPage()
    {
        if (CurrentPage > 1) CurrentPage--;
    }

    private void GroupTransactions()
    {
        var dates = PagedTransactions.Select(x => x.Date).Distinct().ToList();
        foreach (var date in dates)
        {
            var index = PagedTransactions.IndexOf(PagedTransactions.First(x => x.Date == date));
            string label;
            var culture = new CultureInfo("en-US");
            if (date.Day == DateTime.Now.Day) label = "Today - " + date.ToString("MMM dd", culture);
            else if (date.Day == DateTime.Now.AddDays(-1).Day) label = "Yesterday - " + date.ToString("MMM dd", culture);
            else label = date.ToString("MMM dd, yyyy", culture);
            var header = new Transaction { Description = label, Date = date, GroupHeader = true };

            PagedTransactions.Insert(index, header);
        }
    }

    private async Task Initialize()
    {
        try
        {
            await FetchAndInitializeCategories();
            await FetchAndInitializeAccounts();


            var transactions = await DataRepo.General.FetchTransactions();
            _allTransactions = transactions.OrderByDescending(x => x.Date).ToList();

            CalculateMonthlyFinancials();

            CurrentPage = 1;
            OnPropertyChanged(nameof(TotalPages));
            ResetFilters();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private async Task FetchAndInitializeCategories()
    {
        var categories = await DataRepo.General.FetchCategories();
        Categories = new ObservableCollection<Category>(categories.OrderBy(x => x.CreatedAt));
        Categories.Insert(0, new Category() { Name = "All Categories" });
        SelectedCategory = Categories.First();
    }

    private async Task FetchAndInitializeAccounts()
    {
        var accounts = await DataRepo.General.FetchAccounts();
        Accounts = new ObservableCollection<Account>(accounts.OrderBy(x => x.CreatedAt));
        Accounts.Insert(0, new Account() { Name = "All Accounts" });
        SelectedAccount = Accounts.First();
    }

    private void CalculateMonthlyFinancials()
    {
        TotalExpenses = _allTransactions.Where(x => x.Type == "expense" && x.Date.Month == DateTime.Now.Month).Sum(x => Convert.ToDouble(x.Amount));
        TotalIncome = _allTransactions.Where(x => x.Type == "income" && x.Date.Month == DateTime.Now.Month).Sum(x => Convert.ToDouble(x.Amount));
        ExpensesCount = _allTransactions.Count(x => x.Type == "expense" && x.Date.Month == DateTime.Now.Month);
        IncomeCount = _allTransactions.Count(x => x.Type == "income" && x.Date.Month == DateTime.Now.Month);
    }

    public static List<T> GetSurrounding<T>(List<T> list, T item, int count = 5)
    {
        var index = list.IndexOf(item);
        if (index == -1) return new List<T>();

        var half = count / 2;
        var start = Math.Max(0, index - half);
        var end = Math.Min(list.Count, start + count);

        // shift start back if end hit the boundary
        start = Math.Max(0, end - count);

        return list.GetRange(start, end - start);
    }
}