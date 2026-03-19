using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Clario.Data;
using Clario.Models.GeneralModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Clario.Services;

namespace Clario.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public DashboardViewModel _dashboardViewModel;
    public TransactionsViewModel _transactionsViewModel;
    public AccountsViewModel _accountsViewModel;
    public BudgetViewModel _budgetViewModel;
    [ObservableProperty] public Profile? _profile;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(isOnDashboard), nameof(isOnTransactions), nameof(isOnAccounts), nameof(isOnBudget))]
    private ViewModelBase? _currentView;

    [ObservableProperty] private bool _isDarkTheme;

    public MainViewModel()
    {
        _dashboardViewModel = new DashboardViewModel() { parentViewModel = this };
        _transactionsViewModel = new TransactionsViewModel() { parentViewModel = this };
        _accountsViewModel = new AccountsViewModel() { parentViewModel = this };
        _budgetViewModel = new BudgetViewModel() { parentViewModel = this };
        CurrentView = _dashboardViewModel;
        // CurrentView = _transactionsViewModel;
        IsDarkTheme = ThemeService.IsDarkTheme;

        _ = InitializeApp();
    }


    private async Task InitializeApp()
    {
        Profile = await DataRepo.General.FetchProfileInfo();
        _ = await DataRepo.General.FetchCategories();
        _ = await DataRepo.General.FetchAccounts();
        ThemeService.SwitchToTheme(Profile?.Theme ?? "system");
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