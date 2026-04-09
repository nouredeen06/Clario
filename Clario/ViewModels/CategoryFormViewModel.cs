using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Clario.Data;
using Clario.Models;
using Clario.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clario.ViewModels;

public partial class CategoryFormViewModel : ViewModelBase
{
    public required ViewModelBase parentViewModel;

    //  Mode 
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormTitle), nameof(FormSubtitle), nameof(SaveButtonLabel), nameof(CanDelete))]
    private bool _isEditMode = false;

    public string FormTitle => IsEditMode ? "Edit Category" : "New Category";
    public string FormSubtitle => IsEditMode ? "Update the details below" : "Fill in the details below";
    public string SaveButtonLabel => IsEditMode ? "Save Changes" : "Save Category";

    //  Fields 
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsValid))]
    private string _name = "";

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsExpense), nameof(IsIncome))]
    private string _type = "expense";

    [ObservableProperty] private string _selectedIcon = "utensils";

    [ObservableProperty] private string _selectedColor = "#7B9CFF";

    //  Icon options 
    public List<string> CategoryIcons { get; } = new()
    {
        // Food & Dining
        "utensils", "hamburger", "coffee", "pizza", "wine",
        // Shopping
        "shopping-cart", "shopping-bag", "package", "gift", "shirt",
        // Transport
        "car", "bus", "train-front", "bike", "plane",
        // Home & Utilities
        "house", "zap", "wifi", "plug-2", "wrench",
        // Health & Fitness
        "heart-pulse", "pill", "dumbbell", "scissors", "stethoscope",
        // Entertainment
        "gamepad-2", "film", "music", "tv", "headphones",
        // Finance
        "banknote", "credit-card", "piggy-bank", "wallet", "hand-coins",
        "trending-up", "trending-down", "landmark", "circle-dollar-sign", "gem",
        // Work & Education
        "briefcase", "graduation-cap", "book-open", "target", "mail",
        // Personal & Lifestyle
        "heart", "moon", "sun", "leaf", "camera",
        // Bills & Subscriptions
        "receipt", "receipt-text", "smartphone", "volume-2", "refresh-cw",
    };

    //  Validation 
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool IsExpense => Type == "expense";
    public bool IsIncome => Type == "income";
    public bool IsValid => !string.IsNullOrWhiteSpace(Name);
    public bool CanDelete => IsEditMode && DataRepo.General.Categories.Count > 4;

    //  Delete confirm sub-modal 
    [ObservableProperty] private bool _showDeleteConfirm = false;

    //  Callbacks 
    public Action? OnSaved;
    public Action? OnCancelled;
    public Action? OnDeleted;

    //  Edit mode: original category 
    private Guid? _editingId;

    //  Commands 

    [RelayCommand]
    private void SetType(string type) => Type = type;

    [RelayCommand]
    private void SetIcon(string icon) => SelectedIcon = icon;

    [RelayCommand]
    private async Task Save()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Name is required.";
            return;
        }

        try
        {
            if (IsEditMode && _editingId.HasValue)
            {
                var updated = new Category
                {
                    Id = _editingId.Value,
                    UserId = Guid.Parse(SupabaseService.Client.Auth.CurrentUser!.Id),
                    Name = Name.Trim(),
                    Type = Type,
                    Icon = SelectedIcon,
                    Color = SelectedColor,
                };
                await DataRepo.General.UpdateCategory(updated);
            }
            else
            {
                var category = new Category
                {
                    Id = Guid.NewGuid(),
                    UserId = Guid.Parse(SupabaseService.Client.Auth.CurrentUser!.Id!),
                    Name = Name.Trim(),
                    Type = Type,
                    Icon = SelectedIcon,
                    Color = SelectedColor,
                };
                await DataRepo.General.InsertCategory(category);
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
    private void Cancel() => OnCancelled?.Invoke();

    [RelayCommand]
    private void RequestDelete() => ShowDeleteConfirm = true;

    [RelayCommand]
    private void CancelDelete() => ShowDeleteConfirm = false;

    [RelayCommand]
    private async Task ConfirmDelete()
    {
        if (!IsEditMode || !_editingId.HasValue) return;

        try
        {
            await DataRepo.General.DeleteCategory(_editingId.Value);
            OnDeleted?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to delete category.";
            DebugLogger.Log(ex);
        }
    }

    //  Public setup methods 

    public void SetupForAdd()
    {
        ShowDeleteConfirm = false;
        IsEditMode = false;
        _editingId = null;
        Name = "";
        Type = "expense";
        SelectedIcon = "utensils";
        SelectedColor = "#7B9CFF";
        ErrorMessage = null;
    }

    public void SetupForEdit(Category category)
    {
        ShowDeleteConfirm = false;
        IsEditMode = true;
        _editingId = category.Id;
        Name = category.Name;
        Type = category.Type;
        SelectedIcon = category.Icon;
        SelectedColor = category.Color;
        ErrorMessage = null;
        OnPropertyChanged(nameof(CanDelete));
    }
}
