using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Clario.Data;
using Clario.Models;
using Clario.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clario.ViewModels;

public partial class TransactionFormViewModel : ViewModelBase
{
    public required ViewModelBase parentViewModel;
    public GeneralDataRepo AppData => DataRepo.General;

    // ── Mode ────────────────────────────────────────────────
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(FormTitle), nameof(FormSubtitle), nameof(SaveButtonLabel))]
    private bool _isEditMode = false;

    public string FormTitle => IsEditMode ? (IsTransfer ? "Edit Transfer" : "Edit Transaction") : (IsTransfer ? "New Transfer" : "New Transaction");
    public string FormSubtitle => IsEditMode ? "Update the details below" : "Fill in the details below";
    public string SaveButtonLabel => IsEditMode ? "Save Changes" : (IsTransfer ? "Save Transfer" : "Save Transaction");

    // ── Fields ──────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExpense), nameof(IsIncome), nameof(IsTransfer), nameof(IsValid), nameof(FormTitle), nameof(SaveButtonLabel))]
    private string _type = "expense";

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsValid))]
    private string _amount = "";

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsValid))]
    private string _description = "";

    [ObservableProperty] private string? _note;
    [ObservableProperty] private List<DateTime> _dates = [DateTime.Now];
    [ObservableProperty] private DateTime? _selectedDate;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CurrencySymbol))]
    private string _currency = "USD";

    public string CurrencySymbol => CurrencyService.GetSymbol(Currency);

    [ObservableProperty] private string _exchangeRate = "";
    [ObservableProperty] private bool _isFetchingRate = false;
    [ObservableProperty] private bool _showExchangeRateField = false;

    public string ExchangeRateLabel =>
        $"1 {SelectedAccount?.Currency ?? "?"} = ? {AppData.PrimaryAccount?.Currency ?? AppData.Profile?.Currency ?? "USD"}";

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsValid))]
    private Category? _selectedCategory;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsValid))]
    private Account? _selectedAccount;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsValid))]
    private Account? _selectedToAccount;

    [ObservableProperty] private ObservableCollection<Category> _categories = new();
    [ObservableProperty] private ObservableCollection<Account> _accounts = new();

    // ── Validation ──────────────────────────────────────────
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool IsExpense => Type == "expense";
    public bool IsIncome => Type == "income";
    public bool IsTransfer => Type == "transfer";

    public bool IsValid =>
        decimal.TryParse(Amount, out var amt) && amt > 0 &&
        Dates is not null &&
        (IsTransfer
            ? SelectedAccount is not null && SelectedToAccount is not null && SelectedAccount.Id != SelectedToAccount.Id
            : !string.IsNullOrWhiteSpace(Description) && SelectedCategory is not null && SelectedAccount is not null);

    // ── Callbacks ───────────────────────────────────────────
    public Action? OnSaved;
    public Action? OnCancelled;
    public Action? OnDeleted;
    public Action? OnOpenCategoryForm;
    public Action<Category>? OnOpenEditCategoryForm;

    [ObservableProperty] private bool _showDeleteConfirm = false;

    // ── Edit mode: original transaction ─────────────────────
    private Transaction? _editingTransaction;
    private Guid? _editingId;
    private Guid? _transferPairId;
    private decimal _editingOriginalAmount;
    private Guid? _editingOriginalCategoryId;

    // ── Result transaction ──────────────────────────────────
    public Transaction? ResultTransaction { get; set; }

    // ── Budget warning ──────────────────────────────────────
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasBudgetWarning), nameof(HasBudgetApproachingWarning))]
    private string? _budgetWarningMessage;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasBudgetWarning), nameof(HasBudgetApproachingWarning))]
    private bool _budgetWarningIsOverBudget;

    public bool HasBudgetWarning => !string.IsNullOrEmpty(BudgetWarningMessage);
    public bool HasBudgetApproachingWarning => HasBudgetWarning && !BudgetWarningIsOverBudget;

    // ── Commands ────────────────────────────────────────────

    partial void OnSelectedCategoryChanged(Category? value)
    {
        if (value is null) return;
        if (value.Type != Type) Type = value.Type;
        CheckBudgetImpact();
    }

    partial void OnAmountChanged(string value)
    {
        CheckBudgetImpact();
    }

    partial void OnDatesChanged(List<DateTime> value)
    {
        CheckBudgetImpact();
    }

    partial void OnTypeChanged(string value)
    {
        if (value == "transfer")
        {
            CheckBudgetImpact();
            return;
        }

        if (value == SelectedCategory?.Type) return;
        SelectedCategory = _categories.FirstOrDefault(c => c.Type == value);
        CheckBudgetImpact();
    }

    partial void OnSelectedAccountChanged(Account? value)
    {
        if (IsTransfer)
        {
            Currency = value?.Currency ?? "USD";
            return;
        }

        var primaryCurrency = AppData.PrimaryAccount?.Currency ?? AppData.Profile?.Currency ?? "USD";
        var accountCurrency = value?.Currency ?? primaryCurrency;
        Currency = accountCurrency;
        var needsRate = !accountCurrency.Equals(primaryCurrency, StringComparison.OrdinalIgnoreCase);
        if (needsRate)
        {
            IsFetchingRate = true;
            ExchangeRate = "";
        }

        ShowExchangeRateField = needsRate;
        OnPropertyChanged(nameof(ExchangeRateLabel));
        if (needsRate)
            _ = FetchExchangeRateAsync(accountCurrency, primaryCurrency);
    }

    private async Task FetchExchangeRateAsync(string from, string to)
    {
        try
        {
            var rate = await CurrencyService.GetExchangeRateAsync(from, to);
            ExchangeRate = rate.HasValue ? rate.Value.ToString("0.##########") : "";
        }
        catch (Exception ex)
        {
            DebugLogger.Log(ex);
        }
        finally
        {
            IsFetchingRate = false;
        }
    }

    private void CheckBudgetImpact()
    {
        BudgetWarningMessage = null;
        BudgetWarningIsOverBudget = false;

        if (IsTransfer) return;
        if (Type != "expense") return;
        if (SelectedCategory is null) return;
        Debug.WriteLine(SelectedCategory.Name);
        if (!decimal.TryParse(Amount, out var newAmt) || newAmt <= 0) return;

        var budget = DataRepo.General.Budgets.FirstOrDefault(b => b.CategoryId == SelectedCategory.Id);
        if (budget is null) return;

        var transactionDate = Dates?.FirstOrDefault() ?? DateTime.Now;
        var transactions = DataRepo.General.Transactions;

        decimal alreadySpent = budget.Period.ToLower() switch
        {
            "monthly" => transactions
                .Where(t => t.CategoryId == budget.CategoryId &&
                            t.Date.Month == transactionDate.Month &&
                            t.Date.Year == transactionDate.Year)
                .Sum(t => t.Amount),
            "quarterly" => transactions
                .Where(t => t.CategoryId == budget.CategoryId &&
                            t.Date.Month >= transactionDate.Month - 3 &&
                            t.Date.Month <= transactionDate.Month &&
                            t.Date.Year == transactionDate.Year)
                .Sum(t => t.Amount),
            "yearly" => transactions
                .Where(t => t.CategoryId == budget.CategoryId &&
                            t.Date.Year == transactionDate.Year)
                .Sum(t => t.Amount),
            _ => 0
        };

        if (IsEditMode && _editingOriginalCategoryId == SelectedCategory.Id)
            alreadySpent -= _editingOriginalAmount;

        var projectedSpent = alreadySpent + newAmt;

        if (projectedSpent > budget.LimitAmount)
        {
            var overBy = projectedSpent - budget.LimitAmount;
            BudgetWarningIsOverBudget = true;
            BudgetWarningMessage = $"This will exceed your {SelectedCategory.Name} budget by ${overBy:N2}";
        }
        else if (budget.LimitAmount > 0 && (double)(projectedSpent / budget.LimitAmount) * 100 >= budget.AlertThreshold)
        {
            var pct = (double)(projectedSpent / budget.LimitAmount) * 100;
            BudgetWarningIsOverBudget = false;
            BudgetWarningMessage = $"This will bring your {SelectedCategory.Name} budget to {pct:N0}% used";
        }
    }

    [RelayCommand]
    private void OpenCategoryForm() => OnOpenCategoryForm?.Invoke();

    [RelayCommand]
    private void OpenEditCategoryForm()
    {
        if (SelectedCategory is not null)
            OnOpenEditCategoryForm?.Invoke(SelectedCategory);
    }

    [RelayCommand]
    private void SetType(string type)
    {
        Type = type;
    }

    [RelayCommand]
    private void SetToday()
    {
        Dates = [DateTime.Now];
    }

    [RelayCommand]
    private async Task Save()
    {
        ErrorMessage = null;

        if (!decimal.TryParse(Amount, out var amt) || amt <= 0)
        {
            ErrorMessage = "Please enter a valid amount.";
            return;
        }

        if (IsTransfer)
        {
            if (SelectedAccount is null || SelectedToAccount is null)
            {
                ErrorMessage = "Please select both accounts.";
                return;
            }

            if (SelectedAccount.Id == SelectedToAccount.Id)
            {
                ErrorMessage = "From and To accounts must be different.";
                return;
            }

            try
            {
                if (IsEditMode && _transferPairId.HasValue)
                    await DataRepo.General.UpdateTransfer(_transferPairId.Value, SelectedAccount.Id, SelectedToAccount.Id, amt, Dates.FirstOrDefault(), Note);
                else
                    await DataRepo.General.InsertTransfer(SelectedAccount.Id, SelectedToAccount.Id, amt, Dates.FirstOrDefault(), Note);
                OnSaved?.Invoke();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Something went wrong. Please try again.";
                DebugLogger.Log(ex);
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(Description))
        {
            ErrorMessage = "Description is required.";
            return;
        }

        if (SelectedCategory is null)
        {
            ErrorMessage = "Please select a category.";
            return;
        }

        if (SelectedAccount is null)
        {
            ErrorMessage = "Please select an account.";
            return;
        }

        try
        {
            if (IsEditMode && _editingId.HasValue)
            {
                decimal? exchangeRateValue = null;
                if (ShowExchangeRateField && decimal.TryParse(ExchangeRate, out var parsedRate) && parsedRate > 0)
                    exchangeRateValue = parsedRate;
                var updated = new Transaction
                {
                    Id = _editingId.Value,
                    UserId = Guid.Parse(SupabaseService.Client.Auth.CurrentUser!.Id),
                    Type = Type,
                    Amount = amt,
                    Description = Description.Trim(),
                    Note = Note?.Trim(),
                    Date = Dates.FirstOrDefault(),
                    CategoryId = SelectedCategory.Id,
                    AccountId = SelectedAccount.Id,
                    Category = SelectedCategory,
                    ExchangeRate = exchangeRateValue,
                };
                await DataRepo.General.UpdateTransaction(updated);
                ResultTransaction = updated;
            }
            else
            {
                decimal? exchangeRateValue = null;
                if (ShowExchangeRateField && decimal.TryParse(ExchangeRate, out var parsedRate) && parsedRate > 0)
                    exchangeRateValue = parsedRate;
                var transaction = new Transaction
                {
                    Id = Guid.NewGuid(),
                    UserId = Guid.Parse(SupabaseService.Client.Auth.CurrentUser!.Id!),
                    Type = Type,
                    Amount = amt,
                    Description = Description.Trim(),
                    Note = Note?.Trim(),
                    Date = Dates.FirstOrDefault(),
                    CategoryId = SelectedCategory.Id,
                    AccountId = SelectedAccount.Id,
                    Category = SelectedCategory,
                    ExchangeRate = exchangeRateValue,
                };
                await DataRepo.General.InsertTransaction(transaction);
                ResultTransaction = transaction;
            }

            OnSaved?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Something went wrong. Please try again.";
            DebugLogger.Log(ex);
        }
    }

    [RelayCommand]
    private async Task ConfirmDelete()
    {
        if (!IsEditMode) return;

        try
        {
            if (IsTransfer && _transferPairId.HasValue)
                await DataRepo.General.DeleteTransfer(_transferPairId.Value);
            else if (_editingId.HasValue)
                await DataRepo.General.DeleteTransaction(_editingId.Value);
            OnDeleted?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to delete transaction.";
            DebugLogger.Log(ex);
        }
    }

    [RelayCommand]
    private void RequestDelete()
    {
        ShowDeleteConfirm = true;
    }

    [RelayCommand]
    private void CancelDelete()
    {
        ShowDeleteConfirm = false;
    }

    [RelayCommand]
    private void Cancel()
    {
        OnCancelled?.Invoke();
    }

    // ── Public setup methods ─────────────────────────────────

    /// <summary>Call this to open the form for adding a new transaction.</summary>
    public void SetupForAdd()
    {
        ShowDeleteConfirm = false;
        IsEditMode = false;
        _editingId = null;
        _transferPairId = null;
        _editingOriginalAmount = 0;
        _editingOriginalCategoryId = null;
        Categories = AppData.Categories;
        var sortedAccounts = new ObservableCollection<Account>(
            AppData.Accounts.Where(a => !a.IsArchived).OrderByDescending(a => a.IsPrimary).ThenBy(a => a.CreatedAt));
        Accounts = sortedAccounts;
        Type = "expense";
        Amount = "";
        Description = "";
        Note = null;
        Dates = [DateTime.Now];
        ErrorMessage = null;
        SelectedCategory = AppData.Categories.Count > 0 ? AppData.Categories[0] : null;
        SelectedAccount = sortedAccounts.Count > 0 ? sortedAccounts[0] : null;
        SelectedToAccount = sortedAccounts.Count > 1 ? sortedAccounts[1] : null;
        ShowExchangeRateField = false;
        ExchangeRate = "";
        IsFetchingRate = false;
        ResultTransaction = null;
        BudgetWarningMessage = null;
        BudgetWarningIsOverBudget = false;
    }

    /// <summary>Call this to open the form for editing an existing transaction.</summary>
    public void SetupForEdit(Transaction transaction)
    {
        ShowDeleteConfirm = false;
        IsEditMode = true;
        _editingId = transaction.Id;
        _editingOriginalAmount = transaction.Amount;
        _editingOriginalCategoryId = transaction.CategoryId;
        Categories = AppData.Categories;
        var sortedAccounts = new ObservableCollection<Account>(
            AppData.Accounts.Where(a => !a.IsArchived).OrderByDescending(a => a.IsPrimary).ThenBy(a => a.CreatedAt));
        Accounts = sortedAccounts;
        Amount = transaction.Amount.ToString("0.00");
        Note = transaction.Note;
        Dates = [transaction.Date];
        ErrorMessage = null;
        ResultTransaction = transaction;

        if (transaction.IsTransfer && transaction.TransferPairId.HasValue)
        {
            _transferPairId = transaction.TransferPairId;
            Type = "transfer";
            // Find the counterpart to determine from/to
            var counterpart = AppData.Transactions.FirstOrDefault(t => t.TransferPairId == transaction.TransferPairId && t.Id != transaction.Id);
            var outTx = transaction.IsTransferOut ? transaction : counterpart;
            var inTx = transaction.IsTransferOut ? counterpart : transaction;
            SelectedAccount = AppData.Accounts.FirstOrDefault(a => a.Id == outTx?.AccountId) ?? sortedAccounts.FirstOrDefault();
            SelectedToAccount = AppData.Accounts.FirstOrDefault(a => a.Id == inTx?.AccountId) ?? sortedAccounts.Skip(1).FirstOrDefault();
            Description = "Transfer";
            SelectedCategory = null;
            ShowExchangeRateField = false;
            ExchangeRate = "";
            IsFetchingRate = false;
        }
        else
        {
            _transferPairId = null;
            Type = transaction.Type;
            Description = transaction.Description;
            SelectedCategory = AppData.Categories.FirstOrDefault(c => c.Id == transaction.CategoryId)
                               ?? (AppData.Categories.Count > 0 ? AppData.Categories[0] : null);
            SelectedAccount = AppData.Accounts.FirstOrDefault(a => a.Id == transaction.AccountId)
                              ?? (sortedAccounts.Count > 0 ? sortedAccounts[0] : null);
            SelectedToAccount = sortedAccounts.Count > 1 ? sortedAccounts[1] : null;
            if (transaction.ExchangeRate.HasValue)
            {
                ShowExchangeRateField = true;
                ExchangeRate = transaction.ExchangeRate.Value.ToString("0.##########");
            }
            else
            {
                ShowExchangeRateField = false;
                ExchangeRate = "";
            }

            IsFetchingRate = false;
        }

        CheckBudgetImpact();
    }
}