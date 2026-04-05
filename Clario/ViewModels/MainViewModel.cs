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
    private AnalyticsViewModel _analyticsViewModel = null!;

    GeneralDataRepo AppData => DataRepo.General;
    [ObservableProperty] private Profile? _profile;

    [ObservableProperty] private TransactionFormViewModel _transactionFormViewModel = null!;
    [ObservableProperty] private AccountFormViewModel _accountFormViewModel = null!;
    [ObservableProperty] private BudgetFormViewModel _budgetFormViewModel = null!;
    [ObservableProperty] private CategoryFormViewModel _categoryFormViewModel = null!;
    [ObservableProperty] private SettingsViewModel _settingsViewModel = null!;
    [ObservableProperty] private SetSavingsGoalDialogViewModel _setSavingsGoalDialogViewModel = null!;

    [ObservableProperty] private bool _isDimmed;
    [ObservableProperty] private bool _isTransactionFormVisible;
    [ObservableProperty] private bool _isAccountFormVisible;
    [ObservableProperty] private bool _isBudgetFormVisible;
    [ObservableProperty] private bool _isCategoryFormVisible;
    [ObservableProperty] private bool _isSavingsGoalDialogVisible;


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(isOnDashboard), nameof(isOnTransactions), nameof(isOnAccounts), nameof(isOnBudget), nameof(isOnAnalytics), nameof(isOnSettings))]
    private ViewModelBase? _currentView;

    [ObservableProperty] private bool _isDarkTheme;

    public MainViewModel()
    {
        DebugLogger.Log("main vm loaded");
        WeakReferenceMessenger.Default.Register<ProfileUpdated>(this, (_, m) =>
        {
            Profile = AppData.Profile;
            _ = DataRepo.General.RefreshLiveRatesAndEnrich();
        });
        IsDimmed = true;
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
                await DataRepo.General.RefreshLiveRatesAndEnrich();

                DebugLogger.Log("fetched all data");
            });

            AppData.Accounts.CollectionChanged += (_, _) => _ = DataRepo.General.RefreshLiveRatesAndEnrich();

            _dashboardViewModel = new DashboardViewModel()
            {
                parentViewModel = this
            };
            DebugLogger.Log("initialized DashboardViewModel");
            _transactionsViewModel = new TransactionsViewModel()
            {
                parentViewModel = this
            };

            DebugLogger.Log("initialized TransactionsViewModel");
            _accountsViewModel = new AccountsViewModel()
            {
                parentViewModel = this
            };

            DebugLogger.Log("initialized AccountsViewModel");
            _budgetViewModel = new BudgetViewModel()
            {
                parentViewModel = this
            };
            DebugLogger.Log("initialized BudgetViewModel");
            _analyticsViewModel = new AnalyticsViewModel()
            {
                parentViewModel = this
            };
            DebugLogger.Log("initialized AnalyticsViewModel");
            SettingsViewModel = new SettingsViewModel()
            {
                parentViewModel = this
            };
            DebugLogger.Log("initialized SettingsViewModel");
            TransactionFormViewModel = new TransactionFormViewModel()
            {
                parentViewModel = this
            };
            TransactionFormViewModel.OnOpenCategoryForm = OpenAddCategoryFromTransactionForm;
            TransactionFormViewModel.OnOpenEditCategoryForm = OpenEditCategoryFromTransactionForm;
            DebugLogger.Log("initialized TransactionFormViewModel");
            AccountFormViewModel = new AccountFormViewModel()
            {
                parentViewModel = this
            };
            DebugLogger.Log("initialized AccountFormViewModel");
            BudgetFormViewModel = new BudgetFormViewModel()
            {
                parentViewModel = this
            };
            DebugLogger.Log("initialized BudgetFormViewModel");
            CategoryFormViewModel = new CategoryFormViewModel()
            {
                parentViewModel = this
            };
            DebugLogger.Log("initialized CategoryFormViewModel");
            SetSavingsGoalDialogViewModel = new SetSavingsGoalDialogViewModel();
            DebugLogger.Log("initialized SetSavingsGoalDialogViewModel");

            IsDarkTheme = ThemeService.IsDarkTheme;

            ThemeService.SwitchToTheme(AppData.Profile?.Theme ?? "system");
            CurrentView = _dashboardViewModel;
            IsDimmed = false;
        }
        catch (Exception e)
        {
            DebugLogger.Log(e);
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
            DebugLogger.Log(e);
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
            DebugLogger.Log(e);
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
            DebugLogger.Log(e);
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
            DebugLogger.Log(e);
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
            DebugLogger.Log(e);
            throw;
        }
    }

    private void CloseBudgetForm()
    {
        IsDimmed = false;
        IsBudgetFormVisible = false;
    }

    private void OpenEditCategoryFromTransactionForm(Category category)
    {
        CategoryFormViewModel.SetupForEdit(category);
        CategoryFormViewModel.OnSaved = () =>
        {
            TransactionFormViewModel.Categories = AppData.Categories;
            // Keep the selected category in sync after edit
            var updated = AppData.Categories.FirstOrDefault(c => c.Id == category.Id);
            if (updated is not null) TransactionFormViewModel.SelectedCategory = updated;
            CloseCategoryForm();
        };
        CategoryFormViewModel.OnCancelled = CloseCategoryForm;
        CategoryFormViewModel.OnDeleted = () =>
        {
            TransactionFormViewModel.Categories = AppData.Categories;
            TransactionFormViewModel.SelectedCategory = AppData.Categories.FirstOrDefault(c => c.Type == TransactionFormViewModel.Type);
            CloseCategoryForm();
        };
        IsCategoryFormVisible = true;
    }

    // Called by the plus button inside TransactionFormView
    private void OpenAddCategoryFromTransactionForm()
    {
        CategoryFormViewModel.SetupForAdd();
        CategoryFormViewModel.OnSaved = () =>
        {
            // Refresh the category list in the transaction form after adding
            TransactionFormViewModel.Categories = AppData.Categories;
            CloseCategoryForm();
        };
        CategoryFormViewModel.OnCancelled = CloseCategoryForm;
        CategoryFormViewModel.OnDeleted = () =>
        {
            TransactionFormViewModel.Categories = AppData.Categories;
            CloseCategoryForm();
        };
        IsCategoryFormVisible = true;
    }

    [RelayCommand]
    public void OpenAddCategory()
    {
        if (IsDimmed) return;
        CategoryFormViewModel.SetupForAdd();
        CategoryFormViewModel.OnSaved = CloseCategoryForm;
        CategoryFormViewModel.OnCancelled = CloseCategoryForm;
        CategoryFormViewModel.OnDeleted = CloseCategoryForm;
        IsCategoryFormVisible = true;
        IsDimmed = true;
    }

    [RelayCommand]
    public void OpenEditCategory(Category category)
    {
        if (IsDimmed) return;
        CategoryFormViewModel.SetupForEdit(category);
        CategoryFormViewModel.OnSaved = CloseCategoryForm;
        CategoryFormViewModel.OnCancelled = CloseCategoryForm;
        CategoryFormViewModel.OnDeleted = CloseCategoryForm;
        IsCategoryFormVisible = true;
        IsDimmed = true;
    }

    private void CloseCategoryForm()
    {
        IsCategoryFormVisible = false;
        // Only clear the dim if no other modal is open
        if (!IsTransactionFormVisible)
            IsDimmed = false;
    }

    [RelayCommand]
    public void OpenEditSavingsGoal()
    {
        if (IsDimmed) return;
        SetSavingsGoalDialogViewModel.Setup(AppData.Profile?.SavingsGoal);
        SetSavingsGoalDialogViewModel.OnSaved = CloseSavingsGoalDialog;
        SetSavingsGoalDialogViewModel.OnCancelled = CloseSavingsGoalDialog;
        IsSavingsGoalDialogVisible = true;
        IsDimmed = true;
    }

    private void CloseSavingsGoalDialog()
    {
        IsSavingsGoalDialogVisible = false;
        IsDimmed = false;
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
    private void GoToAnalytics()
    {
        CurrentView = _analyticsViewModel;
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
    public bool isOnAnalytics => CurrentView is AnalyticsViewModel;
    public bool isOnSettings => CurrentView is SettingsViewModel;
}