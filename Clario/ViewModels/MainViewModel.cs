using System;
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
using CommunityToolkit.Mvvm.Messaging;

namespace Clario.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private DashboardViewModel _dashboardViewModel = null!;
    public TransactionsViewModel _transactionsViewModel = null!;
    private AccountsViewModel _accountsViewModel = null!;
    private BudgetViewModel _budgetViewModel = null!;

    GeneralDataRepo AppData => DataRepo.General;
    [ObservableProperty] private Profile? _profile;

    [ObservableProperty] private TransactionFormViewModel _transactionFormViewModel = null!;
    [ObservableProperty] private AccountFormViewModel _accountFormViewModel = null!;
    [ObservableProperty] private BudgetFormViewModel _budgetFormViewModel = null!;
    [ObservableProperty] private SettingsViewModel _settingsViewModel = null!;

    [ObservableProperty] private bool _isDimmed;
    [ObservableProperty] private bool _isTransactionFormVisible;
    [ObservableProperty] private bool _isAccountFormVisible;
    [ObservableProperty] private bool _isBudgetFormVisible;


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(isOnDashboard), nameof(isOnTransactions), nameof(isOnAccounts), nameof(isOnBudget), nameof(isOnSettings))]
    private ViewModelBase? _currentView;

    [ObservableProperty] private bool _isDarkTheme;

    public MainViewModel()
    {
        Console.WriteLine("main vm loaded");
        WeakReferenceMessenger.Default.Register<ProfileUpdated>(this, (_, m) => { Profile = AppData.Profile; });
        CurrentView = new LoadingViewModel();
        _ = InitializeApp();
    }


    private async Task InitializeApp()
    {
        try
        {
            await Task.Run(async () =>
            {
                var profilesTask = DataRepo.General.FetchProfileInfo(forceRefresh: true);
                var categoriesTask = DataRepo.General.FetchCategories();
                var transactionsTask = DataRepo.General.FetchTransactions();
                var accountsTask = DataRepo.General.FetchAccounts();
                var budgetsTask = DataRepo.General.FetchBudgets();

                await Task.WhenAll(profilesTask, categoriesTask, accountsTask, transactionsTask, budgetsTask);

                Profile = profilesTask.Result;

                DataRepo.General.LinkTransactionCategories();

                Console.WriteLine("fetched all data");
            });

            _dashboardViewModel = new DashboardViewModel()
            {
                parentViewModel = this
            };
            CurrentView = _dashboardViewModel;

            Console.WriteLine("initialized DashboardViewModel");
            _transactionsViewModel = new TransactionsViewModel()
            {
                parentViewModel = this
            };

            Console.WriteLine("initialized TransactionsViewModel");
            _accountsViewModel = new AccountsViewModel()
            {
                parentViewModel = this
            };

            Console.WriteLine("initialized AccountsViewModel");
            _budgetViewModel = new BudgetViewModel()
            {
                parentViewModel = this
            };
            Console.WriteLine("initialized BudgetViewModel");
            SettingsViewModel = new SettingsViewModel()
            {
                parentViewModel = this
            };
            Console.WriteLine("initialized SettingsViewModel");
            TransactionFormViewModel = new TransactionFormViewModel()
            {
                parentViewModel = this
            };
            Console.WriteLine("initialized TransactionFormViewModel");
            AccountFormViewModel = new AccountFormViewModel()
            {
                parentViewModel = this
            };
            Console.WriteLine("initialized AccountFormViewModel");
            BudgetFormViewModel = new BudgetFormViewModel()
            {
                parentViewModel = this
            };
            Console.WriteLine("initialized BudgetFormViewModel");

            IsDarkTheme = ThemeService.IsDarkTheme;

            ThemeService.SwitchToTheme(AppData.Profile?.Theme ?? "system");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    [RelayCommand]
    public void OpenAddTransaction()
    {
        if (IsDimmed) return;
        try
        {
            TransactionFormViewModel.SetupForAdd();
            TransactionFormViewModel.OnSaved = CloseTransactionForm;
            TransactionFormViewModel.OnCancelled = CloseTransactionForm;
            TransactionFormViewModel.OnDeleted = CloseTransactionForm;
            IsTransactionFormVisible = true;
            IsDimmed = true;
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
        if (IsDimmed) return;
        TransactionFormViewModel.SetupForEdit(transaction);
        TransactionFormViewModel.OnSaved = CloseTransactionForm;
        TransactionFormViewModel.OnCancelled = CloseTransactionForm;
        TransactionFormViewModel.OnDeleted = CloseTransactionForm;
        IsTransactionFormVisible = true;
        IsDimmed = true;
    }

    private void CloseTransactionForm()
    {
        IsTransactionFormVisible = false;
        IsDimmed = false;
    }

    [RelayCommand]
    public void OpenAddAccount()
    {
        if (IsDimmed) return;
        try
        {
            AccountFormViewModel.SetupForAdd();
            AccountFormViewModel.OnSaved = CloseAccountForm;
            AccountFormViewModel.OnCancelled = CloseAccountForm;
            IsAccountFormVisible = true;
            IsDimmed = true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    [RelayCommand]
    public void OpenEditAccount(Account account)
    {
        if (IsDimmed) return;
        try
        {
            AccountFormViewModel.SetupForEdit(account);
            AccountFormViewModel.OnSaved = CloseAccountForm;
            AccountFormViewModel.OnCancelled = CloseAccountForm;
            IsAccountFormVisible = true;
            IsDimmed = true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private void CloseAccountForm()
    {
        IsAccountFormVisible = false;
        IsDimmed = false;
    }

    [RelayCommand]
    private void OpenAddBudget()
    {
        if (IsDimmed) return;
        try
        {
            var unusedCategories = AppData.Categories.Where(x => AppData.Budgets.All(y => y.Category?.Id != x.Id)).ToList();
            BudgetFormViewModel.SetupForAdd(new ObservableCollection<Category>(unusedCategories));
            BudgetFormViewModel.OnSaved = CloseBudgetForm;
            BudgetFormViewModel.OnCancelled = CloseBudgetForm;
            BudgetFormViewModel.OnDeleted = CloseBudgetForm;
            IsBudgetFormVisible = true;
            IsDimmed = true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    [RelayCommand]
    private void OpenEditBudget(Budget budget)
    {
        if (IsDimmed) return;
        try
        {
            var unusedCategories = AppData.Categories.Where(x => AppData.Budgets.All(y => y.Category?.Id != x.Id) || x.Id == budget.CategoryId).ToList();
            BudgetFormViewModel.SetupForEdit(budget, new ObservableCollection<Category>(unusedCategories));
            BudgetFormViewModel.OnSaved = CloseBudgetForm;
            BudgetFormViewModel.OnCancelled = CloseBudgetForm;
            BudgetFormViewModel.OnDeleted = CloseBudgetForm;
            IsBudgetFormVisible = true;
            IsDimmed = true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private void CloseBudgetForm()
    {
        IsDimmed = false;
        IsBudgetFormVisible = false;
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
    private void GoToSettings()
    {
        CurrentView = _settingsViewModel;
    }

    [RelayCommand]
    private async Task SignOut()
    {
        await SupabaseService.Client.Auth.SignOut();
        var user = SupabaseService.Client.Auth.CurrentUser;

        switch (Application.Current?.ApplicationLifetime)
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
    public bool isOnSettings => CurrentView is SettingsViewModel;
}