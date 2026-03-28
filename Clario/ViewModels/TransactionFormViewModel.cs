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

public partial class TransactionFormViewModel : ViewModelBase
{
    public required ViewModelBase parentViewModel;

    // ── Mode ────────────────────────────────────────────────
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(FormTitle), nameof(FormSubtitle), nameof(SaveButtonLabel))]
    private bool _isEditMode = false;

    public string FormTitle => IsEditMode ? "Edit Transaction" : "New Transaction";
    public string FormSubtitle => IsEditMode ? "Update the details below" : "Fill in the details below";
    public string SaveButtonLabel => IsEditMode ? "Save Changes" : "Save Transaction";

    // ── Fields ──────────────────────────────────────────────
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsExpense), nameof(IsIncome), nameof(IsValid))]
    private string _type = "expense";

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsValid))]
    private string _amount = "";

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsValid))]
    private string _description = "";

    [ObservableProperty] private string? _note;
    [ObservableProperty] private List<DateTime> _dates = [DateTime.Now];
    [ObservableProperty] private string _currency = "USD";

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsValid))]
    private Category? _selectedCategory;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsValid))]
    private Account? _selectedAccount;

    [ObservableProperty] private ObservableCollection<Category> _categories = new();
    [ObservableProperty] private ObservableCollection<Account> _accounts = new();

    // ── Validation ──────────────────────────────────────────
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool IsExpense => Type == "expense";
    public bool IsIncome => Type == "income";

    public bool IsValid =>
        decimal.TryParse(Amount, out var amt) && amt > 0 &&
        !string.IsNullOrWhiteSpace(Description) &&
        SelectedCategory is not null &&
        SelectedAccount is not null &&
        Dates is not null;

    // ── Callbacks ───────────────────────────────────────────
    public Action? OnSaved;
    public Action? OnCancelled;
    public Action? OnDeleted;

    [ObservableProperty] private bool _showDeleteConfirm = false;

    // ── Edit mode: original transaction ─────────────────────
    private Transaction? _editingTransaction;
    private Guid? _editingId;

    // ── Result transaction ──────────────────────────────────
    public Transaction? ResultTransaction { get; set; }

    // ── Commands ────────────────────────────────────────────

    partial void OnSelectedCategoryChanged(Category? value)
    {
        if (value.Type == Type) return;
        Type = value.Type;
    }

    partial void OnTypeChanged(string value)
    {
        if (value == SelectedCategory?.Type) return;
        SelectedCategory = _categories.FirstOrDefault(c => c.Type == value);
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
                var updated = new Transaction
                {
                    Id = _editingId.Value,
                    UserId = Guid.Parse(Services.SupabaseService.Client.Auth.CurrentUser!.Id),
                    Type = Type,
                    Amount = amt,
                    Description = Description.Trim(),
                    Note = Note?.Trim(),
                    Date = Dates.FirstOrDefault(),
                    CategoryId = SelectedCategory.Id,
                    AccountId = SelectedAccount.Id,
                    Category = SelectedCategory,
                };
                await DataRepo.General.UpdateTransaction(updated);
                ResultTransaction = updated;
            }
            else
            {
                var transaction = new Transaction
                {
                    Id = Guid.NewGuid(),
                    UserId = Guid.Parse(Services.SupabaseService.Client.Auth.CurrentUser!.Id!),
                    Type = Type,
                    Amount = amt,
                    Description = Description.Trim(),
                    Note = Note?.Trim(),
                    Date = Dates.FirstOrDefault(),
                    CategoryId = SelectedCategory.Id,
                    AccountId = SelectedAccount.Id,
                    Category = SelectedCategory,
                };
                await DataRepo.General.InsertTransaction(transaction);
                ResultTransaction = transaction;
            }

            OnSaved?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Something went wrong. Please try again.";
            Console.WriteLine(ex);
        }
    }

    [RelayCommand]
    private async Task ConfirmDelete()
    {
        if (!IsEditMode || !_editingId.HasValue) return;

        try
        {
            await DataRepo.General.DeleteTransaction(_editingId.Value);
            OnDeleted?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to delete transaction.";
            Console.WriteLine(ex);
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
    public void SetupForAdd(
        ObservableCollection<Category> categories,
        ObservableCollection<Account> accounts)
    {
        ShowDeleteConfirm = false;
        IsEditMode = false;
        _editingId = null;
        Categories = categories;
        Accounts = accounts;
        Type = "expense";
        Amount = "";
        Description = "";
        Note = null;
        Dates = [DateTime.Now];
        ErrorMessage = null;
        SelectedCategory = categories.Count > 0 ? categories[0] : null;
        SelectedAccount = accounts.Count > 0 ? accounts[0] : null;
        ResultTransaction = null;
    }

    /// <summary>Call this to open the form for editing an existing transaction.</summary>
    public void SetupForEdit(
        Transaction transaction,
        ObservableCollection<Category> categories,
        ObservableCollection<Account> accounts)
    {
        ShowDeleteConfirm = false;
        IsEditMode = true;
        _editingId = transaction.Id;
        Categories = categories;
        Accounts = accounts;
        Type = transaction.Type;
        Amount = transaction.Amount.ToString("0.00");
        Description = transaction.Description;
        Note = transaction.Note;
        Dates = [transaction.Date];
        ErrorMessage = null;
        SelectedCategory = categories.FirstOrDefault(c => c.Id == transaction.CategoryId)
                           ?? (categories.Count > 0 ? categories[0] : null);
        SelectedAccount = accounts.FirstOrDefault(a => a.Id == transaction.AccountId)
                          ?? (accounts.Count > 0 ? accounts[0] : null);
        ResultTransaction = transaction;
    }
}