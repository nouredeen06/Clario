using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Clario.Data;
using Clario.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clario.ViewModels;

public partial class AccountsViewModel : ViewModelBase
{
    public required ViewModelBase parentViewModel;

    public GeneralDataRepo AppData => DataRepo.General;

    [ObservableProperty] private ObservableCollection<Account> _visibleAccounts = new();
    [ObservableProperty] private decimal _totalBalance;
    [ObservableProperty] private Account? _selectedAccount;
    [ObservableProperty] private bool _isAccountDeletionConfirmationVisible;
    public bool CanDeleteAccount => VisibleAccounts.Count > 1;

    [ObservableProperty] private bool _isDeleteDialogVisible;
    [ObservableProperty] private DeleteAccountDialogViewModel _deleteDialog = new();

    public AccountsViewModel()
    {
        AppData.Accounts.CollectionChanged += (_, _) => { Initialize(); };
        Initialize();
    }

    public void Initialize()
    {
        FetchAndProcessAccountInfo();
        GroupAccounts();
        SelectedAccount = VisibleAccounts.FirstOrDefault(x => !x.GroupHeader);
    }

    private void FetchAndProcessAccountInfo()
    {
        foreach (var account in AppData.Accounts)
        {
            var accountTransactions = AppData.Transactions.Where(t => t.AccountId == account.Id).ToList();
            account.TransactionsCount = accountTransactions.Count;
            account.CurrentBalance = account.OpeningBalance + accountTransactions.Sum(t => t.Type == "income" ? t.Amount : -t.Amount);
            account.TotalIncomeThisMonth = accountTransactions.Where(t => t.Date.Month == DateTime.Now.Month && t.Type == "income").Sum(t => t.Amount);
            account.TotalExpenseThisMonth = accountTransactions.Where(t => t.Date.Month == DateTime.Now.Month && t.Type == "expense").Sum(t => t.Amount);
            account.IncomeTransactionsThisMonth = accountTransactions.Count(t => t.Date.Month == DateTime.Now.Month && t.Type == "income");
            account.ExpenseTransactionsThisMonth = accountTransactions.Count(t => t.Date.Month == DateTime.Now.Month && t.Type == "expense");
            account.RecentTransactions = accountTransactions.OrderByDescending(t => t.Date).Take(3).ToList();
            var lastMonthBalance = accountTransactions.Where(t => t.Date.Month == DateTime.Now.AddMonths(-1).Month && t.Type == "income")
                .Sum(t => t.Type == "income" ? t.Amount : -t.Amount);
            account.MonthlyIncrease = account.TotalIncomeThisMonth - account.TotalExpenseThisMonth - lastMonthBalance;
            TotalBalance += account.CurrentBalance;
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
            var accountsOfType = AppData.Accounts.Where(a => a.Type.Equals(type, StringComparison.OrdinalIgnoreCase)).ToList();
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