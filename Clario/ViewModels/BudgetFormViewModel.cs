using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Clario.Data;
using Clario.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clario.ViewModels;

public partial class BudgetFormViewModel : ViewModelBase
{
    public required ViewModelBase parentViewModel;

    // ── Mode ────────────────────────────────────────────────
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(FormTitle), nameof(FormSubtitle), nameof(SaveButtonLabel))]
    private bool _isEditMode = false;

    public string FormTitle => IsEditMode ? "Edit Budget" : "New Budget";
    public string FormSubtitle => IsEditMode ? "Update the details below" : "Fill in the details below";
    public string SaveButtonLabel => IsEditMode ? "Save Changes" : "Save Budget";

    // ── Fields ──────────────────────────────────────────────
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsMonthly), nameof(IsQuarterly), nameof(IsYearly), nameof(IsValid))]
    private string _period = "monthly";

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsValid))]
    private string _limitAmount = "";

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsValid))]
    private Category? _selectedCategory;

    [ObservableProperty] private ObservableCollection<Category> _categories = new();

    // AlertThreshold: 0–100 int, stored as double for Slider binding
    // Slider.Value is double; we round to int when saving
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(AlertThresholdLabel))]
    private double _alertThreshold = 80;

    public string AlertThresholdLabel => $"{(int)AlertThreshold}%";

    [ObservableProperty] private bool _rollover = false;

    // ── Validation ──────────────────────────────────────────
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool IsMonthly => Period == "monthly";
    public bool IsQuarterly => Period == "quarterly";
    public bool IsYearly => Period == "yearly";

    public bool IsValid =>
        decimal.TryParse(LimitAmount, out var amt) && amt > 0 &&
        SelectedCategory is not null;

    // ── Callbacks ───────────────────────────────────────────
    public Action? OnSaved;
    public Action? OnCancelled;
    public Action? OnDeleted;

    [ObservableProperty] private bool _showDeleteConfirm = false;

    // ── Edit mode: original budget ───────────────────────────
    private Guid? _editingId;

    // ── Result ──────────────────────────────────────────────
    public Budget? ResultBudget { get; set; }

    // ── Commands ────────────────────────────────────────────

    [RelayCommand]
    private void SetPeriod(string period)
    {
        Period = period;
    }

    [RelayCommand]
    private async Task Save()
    {
        ErrorMessage = null;

        if (!decimal.TryParse(LimitAmount, out var amt) || amt <= 0)
        {
            ErrorMessage = "Please enter a valid amount.";
            return;
        }

        if (SelectedCategory is null)
        {
            ErrorMessage = "Please select a category.";
            return;
        }

        try
        {
            if (IsEditMode && _editingId.HasValue)
            {
                var updated = new Budget
                {
                    Id = _editingId.Value,
                    UserId = Guid.Parse(Services.SupabaseService.Client.Auth.CurrentUser!.Id),
                    CategoryId = SelectedCategory.Id,
                    LimitAmount = amt,
                    Period = Period,
                    AlertThreshold = (int)Math.Round(AlertThreshold),
                    Rollover = Rollover,
                    Category = SelectedCategory,
                };
                await DataRepo.General.UpdateBudget(updated);
                ResultBudget = updated;
            }
            else
            {
                var budget = new Budget
                {
                    Id = Guid.NewGuid(),
                    UserId = Guid.Parse(Services.SupabaseService.Client.Auth.CurrentUser!.Id!),
                    CategoryId = SelectedCategory.Id,
                    LimitAmount = amt,
                    Period = Period,
                    AlertThreshold = (int)Math.Round(AlertThreshold),
                    Rollover = Rollover,
                    Category = SelectedCategory,
                };
                await DataRepo.General.InsertBudget(budget);
                ResultBudget = budget;
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
            await DataRepo.General.DeleteBudget(_editingId.Value);
            OnDeleted?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to delete budget.";
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

    /// <summary>Call this to open the form for adding a new budget.</summary>
    public void SetupForAdd(ObservableCollection<Category> categories)
    {
        ShowDeleteConfirm = false;
        IsEditMode = false;
        _editingId = null;
        Categories = categories;
        LimitAmount = "";
        Period = "monthly";
        AlertThreshold = 80;
        Rollover = false;
        ErrorMessage = null;
        SelectedCategory = categories.Count > 0 ? categories[0] : null;
        ResultBudget = null;
    }

    /// <summary>Call this to open the form for editing an existing budget.</summary>
    public void SetupForEdit(Budget budget, ObservableCollection<Category> categories)
    {
        ShowDeleteConfirm = false;
        IsEditMode = true;
        _editingId = budget.Id;
        Categories = categories;
        LimitAmount = budget.LimitAmount.ToString("0.00");
        Period = budget.Period;
        AlertThreshold = budget.AlertThreshold;
        Rollover = budget.Rollover;
        ErrorMessage = null;
        SelectedCategory = categories.FirstOrDefault(c => c.Id == budget.CategoryId)
                           ?? (categories.Count > 0 ? categories[0] : null);
        ResultBudget = budget;
    }
}