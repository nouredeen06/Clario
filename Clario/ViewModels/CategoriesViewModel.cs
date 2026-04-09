using System;
using System.Collections.ObjectModel;
using System.Linq;
using Clario.Data;
using Clario.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clario.ViewModels;

public partial class CategoriesViewModel : ViewModelBase
{
    public required ViewModelBase parentViewModel;
    public GeneralDataRepo AppData => DataRepo.General;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExpenseCategories))]
    private ObservableCollection<Category> _expenseCategories = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIncomeCategories))]
    private ObservableCollection<Category> _incomeCategories = new();

    public bool HasExpenseCategories => ExpenseCategories.Count > 0;
    public bool HasIncomeCategories => IncomeCategories.Count > 0;

    public CategoriesViewModel()
    {
        Track(AppData.Categories, (_, _) => Initialize());
        Initialize();
    }

    public void Initialize()
    {
        ExpenseCategories = new ObservableCollection<Category>(
            AppData.Categories.Where(c => c.Type == "expense").OrderBy(c => c.Name));
        IncomeCategories = new ObservableCollection<Category>(
            AppData.Categories.Where(c => c.Type == "income").OrderBy(c => c.Name));
    }

    [RelayCommand]
    private void EditCategory(Category category)
    {
        if (parentViewModel is MainViewModel mainVm)
            mainVm.OpenEditCategory(category);
    }

    [RelayCommand]
    private void AddCategory()
    {
        if (parentViewModel is MainViewModel mainVm)
            mainVm.OpenAddCategory();
    }
}
