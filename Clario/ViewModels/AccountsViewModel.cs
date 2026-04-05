using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Clario.Data;
using Clario.Models;
using Clario.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clario.ViewModels;

public partial class AccountsViewModel : ViewModelBase
{
    public required ViewModelBase parentViewModel;

    public GeneralDataRepo AppData => DataRepo.General;

    [ObservableProperty] private ObservableCollection<Account> _visibleAccounts = new();
    [ObservableProperty] private decimal _totalBalance;
    public string PrimarySymbol => CurrencyService.GetSymbol(AppData.PrimaryAccount?.Currency ?? AppData.Profile?.Currency ?? "USD");
    [ObservableProperty] private Account? _selectedAccount;
    [ObservableProperty] private bool _isAccountDeletionConfirmationVisible;
    public bool CanDeleteAccount => VisibleAccounts.Count(x => !x.GroupHeader) > 1;
    public int ActiveAccountCount => VisibleAccounts.Count(x => !x.GroupHeader);

    [ObservableProperty] private bool _isDeleteDialogVisible;
    [ObservableProperty] private DeleteAccountDialogViewModel _deleteDialog = new();

    [ObservableProperty] private bool _isArchiveDialogVisible;
    [ObservableProperty] private Account? _accountToArchive;

    [ObservableProperty] private bool _isArchivedListVisible;
    [ObservableProperty] private List<Account> _archivedAccounts = new();
    public bool HasArchivedAccounts => ArchivedAccounts.Count > 0;

    public AccountsViewModel()
    {
        AppData.Accounts.CollectionChanged += (_, _) => { Initialize(); };
        AppData.Transactions.CollectionChanged += (_, _) => { Initialize(); };
        Initialize();
    }

    public void Initialize()
    {
        var prevSelectedId = SelectedAccount?.Id;
        FetchAndProcessAccountInfo();
        GroupAccounts();
        ArchivedAccounts = AppData.Accounts.Where(a => a.IsArchived).ToList();
        OnPropertyChanged(nameof(HasArchivedAccounts));
        OnPropertyChanged(nameof(ActiveAccountCount));
        OnPropertyChanged(nameof(CanDeleteAccount));
        // Set to null first so PropertyChanged fires even when re-selecting the same account,
        // ensuring the detail panel re-reads all computed properties (balance, income, etc.)
        SelectedAccount = null;
        SelectedAccount = (prevSelectedId.HasValue
            ? VisibleAccounts.FirstOrDefault(a => a.Id == prevSelectedId && !a.GroupHeader)
            : null) ?? VisibleAccounts.FirstOrDefault(x => !x.GroupHeader);
    }

    private void FetchAndProcessAccountInfo()
    {
        TotalBalance = 0;
        var primaryCurrency = AppData.PrimaryAccount?.Currency ?? AppData.Profile?.Currency ?? "USD";
        foreach (var account in AppData.Accounts.Where(a => !a.IsArchived))
        {
            var accountTransactions = AppData.Transactions.Where(t => t.AccountId == account.Id).ToList();
            account.TransactionsCount = accountTransactions.Count;
            account.CurrentBalance = account.OpeningBalance + accountTransactions.Sum(t => t.Type is "income" or "transfer_in" ? t.Amount : -t.Amount);
            account.TotalIncomeThisMonth = accountTransactions.Where(t => t.Date.Month == DateTime.Now.Month && t.Type is "income" or "transfer_in").Sum(t => t.Amount);
            account.TotalExpenseThisMonth = accountTransactions.Where(t => t.Date.Month == DateTime.Now.Month && t.Type is "expense" or "transfer_out").Sum(t => t.Amount);
            account.IncomeTransactionsThisMonth = accountTransactions.Count(t => t.Date.Month == DateTime.Now.Month && t.Type is "income" or "transfer_in");
            account.ExpenseTransactionsThisMonth = accountTransactions.Count(t => t.Date.Month == DateTime.Now.Month && t.Type is "expense" or "transfer_out");
            account.RecentTransactions = accountTransactions.OrderByDescending(t => t.Date).Take(3).ToList();
            var lastMonthBalance = accountTransactions.Where(t => t.Date.Month == DateTime.Now.AddMonths(-1).Month && t.Type == "income")
                .Sum(t => t.Type == "income" ? t.Amount : -t.Amount);
            account.MonthlyIncrease = account.TotalIncomeThisMonth - account.TotalExpenseThisMonth - lastMonthBalance;
            if (account.Currency.Equals(primaryCurrency, StringComparison.OrdinalIgnoreCase))
                TotalBalance += account.CurrentBalance;
            else
                TotalBalance += accountTransactions.Sum(t => t.Type is "income" or "transfer_in" ? t.ConvertedAmount : -t.ConvertedAmount);
        }
    }

    [RelayCommand]
    private void CreateAccount()
    {
        ((MainViewModel)parentViewModel).OpenAddAccount();
    }

    [RelayCommand]
    private void EditAccount(Account account)
    {
        ((MainViewModel)parentViewModel).OpenEditAccount(account);
    }


    private void GroupAccounts()
    {
        var accountTypes = new List<string>()
        {
            "Cash",
            "Checking",
            "Savings",
            "Credit",
            "Investment",
            "Other"
        };
        VisibleAccounts.Clear();
        foreach (var type in accountTypes)
        {
            var accountsOfType = AppData.Accounts
                .Where(a => a.Type.Equals(type, StringComparison.OrdinalIgnoreCase) && !a.IsArchived)
                .OrderByDescending(a => a.IsPrimary)
                .ThenBy(a => a.CreatedAt)
                .ToList();
            if (accountsOfType.Any())
            {
                var header = new Account { Name = type.ToUpper(), GroupHeader = true };
                VisibleAccounts.Add(header);
                foreach (var account in accountsOfType)
                {
                    VisibleAccounts.Add(account);
                }
            }
        }

        OnPropertyChanged(nameof(CanDeleteAccount));
    }

    [RelayCommand]
    private void RequestArchiveAccount(Account account)
    {
        AccountToArchive = account;
        IsArchiveDialogVisible = true;
    }

    [RelayCommand]
    private void CancelArchive()
    {
        IsArchiveDialogVisible = false;
        AccountToArchive = null;
    }

    [RelayCommand]
    private async Task ConfirmArchive()
    {
        if (AccountToArchive is null) return;
        AccountToArchive.IsArchived = true;
        await AppData.UpdateAccount(AccountToArchive);
        IsArchiveDialogVisible = false;
        AccountToArchive = null;
        Initialize();
    }

    [RelayCommand]
    private void ShowArchivedList()
    {
        IsArchivedListVisible = true;
    }

    [RelayCommand]
    private void CloseArchivedList()
    {
        IsArchivedListVisible = false;
    }

    [RelayCommand]
    private async Task UnarchiveAccount(Account account)
    {
        account.IsArchived = false;
        await AppData.UpdateAccount(account);
        Initialize();
        if (!HasArchivedAccounts)
            IsArchivedListVisible = false;
    }

    [RelayCommand]
    private void RequestDeleteAccount(Account account)
    {
        DeleteDialog.Setup(account, new ObservableCollection<Account>(AppData.Accounts));
        DeleteDialog.OnDeleted = () =>
        {
            IsDeleteDialogVisible = false;
            Initialize();
        };
        DeleteDialog.OnCancelled = () => IsDeleteDialogVisible = false;
        IsDeleteDialogVisible = true;
    }

    [RelayCommand]
    private void SelectAccount(Account account)
    {
        SelectedAccount = account;
    }

    [RelayCommand]
    private void ShowAccountTransactions()
    {
        if (parentViewModel is MainViewModel mainViewModel)
        {
            if (SelectedAccount is null) return;
            var vm = mainViewModel._transactionsViewModel;
            vm.SelectedAccount = vm.Accounts.First(x => x.Id == SelectedAccount.Id);
            vm.LoadPageCommand.Execute(1);
            mainViewModel.CurrentView = vm;
        }
    }
}