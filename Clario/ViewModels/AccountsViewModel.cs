using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Clario.Data;
using Clario.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clario.ViewModels;

public partial class AccountsViewModel : ViewModelBase
{
    public required ViewModelBase parentViewModel;
    public required List<Account> Accounts = new();
    public required List<Transaction> Transactions = new();
    [ObservableProperty] private ObservableCollection<Account> _visibleAccounts = new();
    [ObservableProperty] private decimal _totalBalance = 0;
    [ObservableProperty] private Account _selectedAccount;

    public AccountsViewModel()
    {
    
    }

    public async Task Initialize()
    {
        FetchAndProcessAccountInfo();
        GroupAccounts();
        SelectedAccount = VisibleAccounts.First(x => !x.GroupHeader);
    }

    private void FetchAndProcessAccountInfo()
    {
        foreach (var account in Accounts)
        {
            var accountTransactions = Transactions.Where(t => t.AccountId == account.Id).ToList();
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

    private void GroupAccounts()
    {
        var accountTypes = new Dictionary<string, string>()
        {
            { "checking", "Cash & Checking" },
            { "savings", "Savings" },
            { "credit", "Credit" },
            { "investment", "Investments" }
        };

        foreach (var type in accountTypes)
        {
            var accountsOfType = Accounts.Where(a => a.Type == type.Key).ToList();
            if (accountsOfType.Any())
            {
                var header = new Account { Name = type.Value.ToUpper(), GroupHeader = true };
                VisibleAccounts.Add(header);
                foreach (var account in accountsOfType)
                {
                    VisibleAccounts.Add(account);
                }
            }
        }
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
            var vm = mainViewModel._transactionsViewModel;
            vm.SelectedAccount = vm.Accounts.First(x => x.Id == SelectedAccount.Id);
            vm.LoadPageCommand.Execute(1);
            mainViewModel.CurrentView = vm;
        }
    }
}