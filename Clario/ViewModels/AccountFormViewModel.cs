using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Clario.Data;
using Clario.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clario.ViewModels;

public partial class AccountFormViewModel : ViewModelBase
{
    public required ViewModelBase parentViewModel;

    // ── Mode ────────────────────────────────────────────────
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(FormTitle), nameof(FormSubtitle), nameof(SaveButtonLabel))]
    private bool _isEditMode = false;

    public string FormTitle => IsEditMode ? "Edit Account" : "New Account";
    public string FormSubtitle => IsEditMode ? "Update the details below" : "Fill in the details below";
    public string SaveButtonLabel => IsEditMode ? "Save Changes" : "Save Account";

    // ── Fields ──────────────────────────────────────────────
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsValid))]
    private string _name = "";

    [ObservableProperty] private string _selectedType = "Checking";

    [ObservableProperty] private string? _institution;

    [ObservableProperty] private string? _mask;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsValid))]
    private string _openingBalance = "0.00";

    [ObservableProperty] private string _currency = "USD";

    [ObservableProperty] private string? _creditLimit;

    [ObservableProperty] private List<DateTime>? _openedAtDates;

    [ObservableProperty] private string _selectedIcon = "wallet";

    [ObservableProperty] private string _selectedColor = "#3B82F6";

    // ── Options ─────────────────────────────────────────────
    [ObservableProperty] private List<string> _accountTypes = new() { "Cash", "Checking", "Savings", "Credit", "Investment", "Other" };

    [ObservableProperty] private List<string> _currencies = new() { "USD", "EUR", "GBP", "CAD", "AUD" };

    [ObservableProperty] private List<string> _icons = new() { "wallet", "credit-card", "banknote", "landmark", "piggy-bank", "dollar-sign" };

    // ── Validation ──────────────────────────────────────────
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Name) &&
        decimal.TryParse(OpeningBalance, out _);

    public bool IsCredit => SelectedType == "Credit";

    // ── Callbacks ───────────────────────────────────────────
    public Action? OnSaved;
    public Action? OnCancelled;

    // ── Edit mode: original account ─────────────────────────
    private Guid? _editingId;

    // ── Result account ──────────────────────────────────────
    public Account? ResultAccount { get; set; }

    // ── Commands ────────────────────────────────────────────

    partial void OnSelectedTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsCredit));
    }

    [RelayCommand]
    private async Task Save()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Name is required.";
            return;
        }

        if (!decimal.TryParse(OpeningBalance, out var balance))
        {
            ErrorMessage = "Please enter a valid opening balance.";
            return;
        }

        decimal? creditLimitValue = null;
        if (IsCredit && !string.IsNullOrWhiteSpace(CreditLimit))
        {
            if (!decimal.TryParse(CreditLimit, out var limit))
            {
                ErrorMessage = "Please enter a valid credit limit.";
                return;
            }

            creditLimitValue = limit;
        }


        try
        {
            if (IsEditMode && _editingId.HasValue)
            {
                var updated = new Account
                {
                    Id = _editingId.Value,
                    UserId = Guid.Parse(Services.SupabaseService.Client.Auth.CurrentUser!.Id),
                    Name = Name.Trim(),
                    Type = SelectedType,
                    Institution = Institution?.Trim(),
                    Mask = Mask?.Trim(),
                    Currency = Currency,
                    OpeningBalance = balance,
                    CreditLimit = creditLimitValue,
                    OpenedAt = OpenedAtDates?[0],
                    Icon = SelectedIcon,
                    Color = SelectedColor,
                };
                await DataRepo.General.UpdateAccount(updated);
                ResultAccount = updated;
            }
            else
            {
                var account = new Account
                {
                    Id = Guid.NewGuid(),
                    UserId = Guid.Parse(Services.SupabaseService.Client.Auth.CurrentUser!.Id!),
                    Name = Name.Trim(),
                    Type = SelectedType,
                    Institution = Institution?.Trim(),
                    Mask = Mask?.Trim(),
                    Currency = Currency,
                    OpeningBalance = balance,
                    CreditLimit = creditLimitValue,
                    OpenedAt = OpenedAtDates?[0],
                    Icon = SelectedIcon,
                    Color = SelectedColor,
                };
                var result = await DataRepo.General.InsertAccount(account);
                ResultAccount = result;
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
    private void Cancel()
    {
        OnCancelled?.Invoke();
    }

    // ── Public setup methods ─────────────────────────────────

    /// <summary>Call this to open the form for adding a new account.</summary>
    public void SetupForAdd()
    {
        IsEditMode = false;
        _editingId = null;
        Name = "";
        SelectedType = "Checking";
        Institution = null;
        Mask = null;
        OpeningBalance = "0.00";
        Currency = DataRepo.General.Profile?.Currency ?? "USD";
        CreditLimit = null;
        OpenedAtDates = null;
        SelectedIcon = "wallet";
        SelectedColor = "#3B82F6";
        ErrorMessage = null;
        ResultAccount = null;
    }

    /// <summary>Call this to open the form for editing an existing account.</summary>
    public void SetupForEdit(Account account)
    {
        IsEditMode = true;
        _editingId = account.Id;
        Name = account.Name;
        SelectedType = account.Type;
        Institution = account.Institution;
        Mask = account.Mask;
        OpeningBalance = account.OpeningBalance.ToString("0.00");
        Currency = account.Currency;
        CreditLimit = account.CreditLimit?.ToString("0.00");
        OpenedAtDates = account.OpenedAt.HasValue ? new List<DateTime> { account.OpenedAt.Value } : null;
        SelectedIcon = account.Icon;
        SelectedColor = account.Color;
        ErrorMessage = null;
        ResultAccount = account;
    }
}