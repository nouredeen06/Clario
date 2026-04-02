using System;
using System.Globalization;
using System.Threading.Tasks;
using Clario.Data;
using Clario.Models.GeneralModels;
using Clario.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clario.ViewModels;

public partial class SetSavingsGoalDialogViewModel : ViewModelBase
{
    public GeneralDataRepo AppData => DataRepo.General;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsValid))]
    private string _goalInput = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool IsValid =>
        decimal.TryParse(GoalInput, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) && v >= 0;

    // ── Callbacks ────────────────────────────────────────────
    public Action? OnSaved;
    public Action? OnCancelled;

    // ── Setup ────────────────────────────────────────────────
    public void Setup(decimal? currentGoal)
    {
        GoalInput = currentGoal.HasValue
            ? currentGoal.Value.ToString("F2", CultureInfo.InvariantCulture)
            : string.Empty;
        ErrorMessage = null;
    }

    // ── Commands ─────────────────────────────────────────────

    [RelayCommand]
    private void Cancel() => OnCancelled?.Invoke();

    [RelayCommand]
    private async Task Save()
    {
        if (!decimal.TryParse(GoalInput, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount) || amount < 0)
        {
            ErrorMessage = "Please enter a valid amount.";
            return;
        }

        ErrorMessage = null;

        try
        {
            await DataRepo.General.UpdateSavingsGoal(amount);
            OnSaved?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to save savings goal. Please try again.";
            DebugLogger.Log(ex);
        }
    }

    [RelayCommand]
    private async Task Clear()
    {
        ErrorMessage = null;
        try
        {
            await DataRepo.General.UpdateSavingsGoal(null);
            OnSaved?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to clear savings goal. Please try again.";
            DebugLogger.Log(ex);
        }
    }
}
