using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Clario.Data;
using Clario.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clario.ViewModels;

public partial class DeleteAccountDialogViewModel : ViewModelBase
{
    // ── State machine ────────────────────────────────────────
    public enum DialogStep
    {
        SimpleConfirm,
        HasTransactions,
        Migrate
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(IsSimpleConfirmStep),
        nameof(IsHasTransactionsStep),
        nameof(IsMigrateStep))]
    private DialogStep _currentStep;

    public bool IsSimpleConfirmStep => CurrentStep == DialogStep.SimpleConfirm;
    public bool IsHasTransactionsStep => CurrentStep == DialogStep.HasTransactions;
    public bool IsMigrateStep => CurrentStep == DialogStep.Migrate;

    // ── Data ─────────────────────────────────────────────────
    [ObservableProperty] private Account? _account;
    public GeneralDataRepo AppData => DataRepo.General;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanMigrateAndDelete))]
    private Account? _targetAccount;

    [ObservableProperty] private ObservableCollection<Account> _availableAccounts = new();

    // ── Validation ───────────────────────────────────────────
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool CanMigrateAndDelete =>
        TargetAccount is not null &&
        TargetAccount.Id != Account?.Id;

    // ── Callbacks ────────────────────────────────────────────
    public Action? OnDeleted;
    public Action? OnCancelled;

    // ── Setup ────────────────────────────────────────────────

    /// <summary>
    /// Call this to open the dialog for a specific account.
    /// Automatically determines whether to show simple confirm or migrate warning.
    /// </summary>
    public void Setup(Account account, ObservableCollection<Account> allAccounts)
    {
        Account = account;
        ErrorMessage = null;

        // filter out the account being deleted from target options
        var others = allAccounts
            .Where(a => a.Id != account.Id && !a.GroupHeader)
            .ToList();

        AvailableAccounts = new ObservableCollection<Account>(others);
        TargetAccount = AvailableAccounts.FirstOrDefault();

        // decide which step to show based on transaction count
        CurrentStep = account.TransactionsCount > 0
            ? DialogStep.HasTransactions
            : DialogStep.SimpleConfirm;
    }

    // ── Commands ─────────────────────────────────────────────

    [RelayCommand]
    private void Cancel() => OnCancelled?.Invoke();

    [RelayCommand]
    private void GoToMigrateStep()
    {
        ErrorMessage = null;
        CurrentStep = DialogStep.Migrate;
    }

    [RelayCommand]
    private void BackToWarning()
    {
        ErrorMessage = null;
        CurrentStep = DialogStep.HasTransactions;
    }

    [RelayCommand]
    private async Task ConfirmDelete()
    {
        if (Account is null) return;
        ErrorMessage = null;

        try
        {
            await DataRepo.General.DeleteAccount(Account.Id);
            OnDeleted?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to delete account. Please try again.";
            Console.WriteLine(ex);
        }
    }

    [RelayCommand]
    private async Task MigrateAndDelete()
    {
        if (Account is null || TargetAccount is null)
        {
            ErrorMessage = "Please select a target account.";
            return;
        }

        if (TargetAccount.Id == Account.Id)
        {
            ErrorMessage = "Target account must be different from the account being deleted.";
            return;
        }

        ErrorMessage = null;

        try
        {
            // 1. re-link all transactions from deleted account to target
            await DataRepo.General.MigrateTransactions(Account.Id, TargetAccount.Id);

            // 2. recalculate balances on both accounts
            await DataRepo.General.RecalculateAccountBalance(TargetAccount.Id);

            // 3. delete the account
            await DataRepo.General.DeleteAccount(Account.Id);

            OnDeleted?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Migration failed. Please try again.";
            Console.WriteLine(ex);
        }
    }
}