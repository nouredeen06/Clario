using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Clario.Data;
using Clario.Models;
using Clario.Models.GeneralModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Clario.Services;

namespace Clario.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private DashboardViewModel _dashboardViewModel;
    public TransactionsViewModel _transactionsViewModel;
    private AccountsViewModel _accountsViewModel;
    private BudgetViewModel _budgetViewModel;
    [ObservableProperty] private TransactionFormViewModel _transactionFormViewModel;
    [ObservableProperty] public Profile? _profile;
    private List<Transaction> _transactions = new();
    private List<Category> _categories = new();
    private List<Budget> _budgets = new();
    private List<Account> _accounts = new();

    [ObservableProperty] private bool _isTransactionFormVisible;


    [ObservableProperty] [NotifyPropertyChangedFor(nameof(isOnDashboard), nameof(isOnTransactions), nameof(isOnAccounts), nameof(isOnBudget))]
    private ViewModelBase? _currentView;

    [ObservableProperty] private bool _isDarkTheme;

    public MainViewModel()
    {
        Console.WriteLine("main vm loaded");
        CurrentView = new LoadingViewModel();
        _ = InitializeApp();
    }


    private async Task InitializeApp()
    {
        try
        {
            var profilesTask = DataRepo.General.FetchProfileInfo();
            var categoriesTask = DataRepo.General.FetchCategories();
            var accountsTask = DataRepo.General.FetchAccounts();
            var transactionsTask = DataRepo.General.FetchTransactions();
            var budgetsTask = DataRepo.General.FetchBudgets();

            await Task.WhenAll(profilesTask, categoriesTask, accountsTask, transactionsTask, budgetsTask);

            Profile = profilesTask.Result;
            _categories = categoriesTask.Result;
            _accounts = accountsTask.Result;
            _transactions = transactionsTask.Result;
            _budgets = budgetsTask.Result;

            Console.WriteLine("fetched all data");

            _dashboardViewModel = new DashboardViewModel()
            {
                parentViewModel = this,
                Transactions = _transactions,
                Categories = _categories,
                Accounts = _accounts,
                Budgets = _budgets
            };
            _dashboardViewModel.initialize();
            CurrentView = _dashboardViewModel;

            Console.WriteLine("initialized DashboardViewModel");
            _transactionsViewModel = new TransactionsViewModel()
            {
                parentViewModel = this,
                AllTransactions = _transactions.OrderByDescending(x => x.Date).ToList(),
                Categories = new ObservableCollection<Category>(_categories.OrderBy(x => x.CreatedAt)),
                Accounts = new ObservableCollection<Account>(_accounts.OrderBy(x => x.CreatedAt))
            };
            await _transactionsViewModel.Initialize();

            Console.WriteLine("initialized TransactionsViewModel");
            _accountsViewModel = new AccountsViewModel()
            {
                parentViewModel = this,
                Accounts = _accounts,
                Transactions = _transactions
            };
            await _accountsViewModel.Initialize();

            Console.WriteLine("initialized AccountsViewModel");
            _budgetViewModel = new BudgetViewModel()
            {
                parentViewModel = this,
                Profile = Profile,
                Budgets = _budgets,
                Categories = _categories,
                Transactions = _transactions
            };
            await _budgetViewModel.Initialize();
            Console.WriteLine("initialized BudgetViewModel");
            TransactionFormViewModel = new TransactionFormViewModel()
            {
                parentViewModel = this
            };
            Console.WriteLine("initialized TransactionFormViewModel");

            IsDarkTheme = ThemeService.IsDarkTheme;

            ThemeService.SwitchToTheme(Profile?.Theme ?? "system");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    [RelayCommand]
    public void OpenAddTransaction()
    {
        if (IsTransactionFormVisible) return;
        try
        {
            TransactionFormViewModel.SetupForAdd(
                new ObservableCollection<Category>(_categories),
                new ObservableCollection<Account>(_accounts)
            );
            TransactionFormViewModel.OnSaved = () =>
            {
                if (TransactionFormViewModel.ResultTransaction is not null)
                {
                    var previousItem = _transactionsViewModel.AllTransactions.FirstOrDefault(x => x.Date < TransactionFormViewModel.ResultTransaction.Date);
                    var index = 0;
                    if (previousItem is not null)
                        index = _transactionsViewModel.AllTransactions.IndexOf(previousItem);
                    if (index == -1) index = 0;
                    _transactionsViewModel.AllTransactions.Insert(index, TransactionFormViewModel.ResultTransaction);
                    _dashboardViewModel.Transactions.Insert(index, TransactionFormViewModel.ResultTransaction);
                    _dashboardViewModel.UpdateUserOverviewCommand.Execute(null);
                    _transactionsViewModel.LoadPageCommand.Execute(1);
                }

                CloseTransactionForm();
            };
            TransactionFormViewModel.OnCancelled = () => CloseTransactionForm();
            TransactionFormViewModel.OnDeleted = () =>
            {
                if (TransactionFormViewModel.ResultTransaction is { } resultTransaction)
                {
                    _transactionsViewModel.AllTransactions.Remove(resultTransaction);
                    _transactionsViewModel.LoadPageCommand.Execute(1);
                }

                CloseTransactionForm();
            };
            IsTransactionFormVisible = true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    [RelayCommand]
    public void OpenEditTransaction(Transaction transaction)
    {
        TransactionFormViewModel.SetupForEdit(
            transaction,
            new ObservableCollection<Category>(_categories),
            new ObservableCollection<Account>(_accounts)
        );
        TransactionFormViewModel.OnSaved = () =>
        {
            if (TransactionFormViewModel.ResultTransaction is { } resultTransaction)
            {
                var index = _transactionsViewModel.AllTransactions.FindIndex(x => x.Id == transaction.Id);
                if (index != -1)
                    _transactionsViewModel.AllTransactions[index] = resultTransaction;
                _transactionsViewModel.LoadPageCommand.Execute(1);
            }

            CloseTransactionForm();
        };
        TransactionFormViewModel.OnCancelled = CloseTransactionForm;
        TransactionFormViewModel.OnDeleted = () =>
        {
            if (TransactionFormViewModel.ResultTransaction is { } resultTransaction)
            {
                _transactionsViewModel.AllTransactions.Remove(resultTransaction);
                _transactionsViewModel.LoadPageCommand.Execute(1);
            }

            CloseTransactionForm();
        };
        IsTransactionFormVisible = true;
    }


    private void CloseTransactionForm()
    {
        IsTransactionFormVisible = false;
    }

    [RelayCommand]
    private void SwitchTheme()
    {
        ThemeService.SwitchToTheme(ThemeService.IsDarkTheme ? ThemeVariant.Light : ThemeVariant.Dark);
        IsDarkTheme = ThemeService.IsDarkTheme;
    }

    [RelayCommand]
    private void GoToDashboard()
    {
        CurrentView = _dashboardViewModel;
    }

    [RelayCommand]
    private void GoToTransactions()
    {
        CurrentView = _transactionsViewModel;
    }

    [RelayCommand]
    private void GoToAccounts()
    {
        CurrentView = _accountsViewModel;
    }

    [RelayCommand]
    private void GoToBudget()
    {
        CurrentView = _budgetViewModel;
    }

    [RelayCommand]
    private async Task SignOut()
    {
        await SupabaseService.Client.Auth.SignOut();
        var user = SupabaseService.Client.Auth.CurrentUser;

        switch (Application.Current.ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                desktop.MainWindow!.DataContext = user is not null ? new MainViewModel() : new AuthViewModel();
                break;
            case ISingleViewApplicationLifetime singleViewPlatform:
                singleViewPlatform.MainView!.DataContext = user is not null ? new MainViewModel() : new AuthViewModel();
                break;
        }
    }

    public bool isOnDashboard => CurrentView is DashboardViewModel;
    public bool isOnTransactions => CurrentView is TransactionsViewModel;
    public bool isOnAccounts => CurrentView is AccountsViewModel;
    public bool isOnBudget => CurrentView is BudgetViewModel;
}