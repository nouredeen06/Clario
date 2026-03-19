using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using Clario.Data;
using Clario.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace Clario.ViewModels;

public partial class BudgetViewModel : ViewModelBase
{
    public required ViewModelBase parentViewModel;

    [ObservableProperty] private List<Budget> _budgets = new();

    public BudgetViewModel()
    {
        _ = Initialize();
    }

    public async Task Initialize()
    {
        var budgets = await DataRepo.General.FetchBudgets();
        var categories = await DataRepo.General.FetchCategories();
        var transactions = await DataRepo.General.FetchTransactions();
        foreach (var budget in budgets)
        {
            budget.Category = categories.FirstOrDefault(x => x.Id == budget.CategoryId);

            switch (budget.Period.ToLower())
            {
                case "monthly":
                    var budgetTransactions = transactions.Where(x => x.Date.Month == DateTime.Now.Month && x.CategoryId == budget.CategoryId).ToList();
                    budget.Spent = budgetTransactions.Sum(x => x.Amount);
                    budget.TransactionsCount = budgetTransactions.Count;
                    break;
                case "quarterly":
                    var quarterTransactions = transactions.Where(x =>
                        x.Date.Month >= DateTime.Now.Month - 3 && x.Date.Month <= DateTime.Now.Month && x.CategoryId == budget.CategoryId).ToList();
                    budget.Spent = quarterTransactions.Sum(x => x.Amount);
                    budget.TransactionsCount = quarterTransactions.Count;
                    break;
                case "yearly":
                    var yearTransactions = transactions.Where(x => x.Date.Year == DateTime.Now.Year && x.CategoryId == budget.CategoryId).ToList();
                    budget.Spent = yearTransactions.Sum(x => x.Amount);
                    budget.TransactionsCount = yearTransactions.Count;
                    break;
                default:
                    break;
            }
        }


        if (budgets.Any(x => x.IsOnTrack))
        {
            Budgets.Add(new Budget() { Category = new Category() { Name = "ON TRACK" }, GroupHeader = true });
            var onTrack = budgets.Where(x => x.IsOnTrack).OrderByDescending(x => x.PercentageUsed).ToList();
            foreach (var budget in onTrack)
            {
                Budgets.Add(budget);
            }
        }

        if (budgets.Any(x => x.IsWarning))
        {
            Budgets.Add(new Budget() { Category = new Category() { Name = "APPROACHING LIMIT" }, GroupHeader = true });
            var approaching = budgets.Where(x => x.IsWarning).OrderByDescending(x => x.PercentageUsed).ToList();
            foreach (var budget in approaching)
            {
                Budgets.Add(budget);
            }
        }

        if (budgets.Any(x => x.IsOverBudget))
        {
            Budgets.Add(new Budget() { Category = new Category() { Name = "OVER BUDGET" }, GroupHeader = true });
            var overBudget = budgets.Where(x => x.IsOverBudget).OrderByDescending(x => x.PercentageUsed).ToList();
            foreach (var budget in overBudget)
            {
                Budgets.Add(budget);
            }
        }

        foreach (var budget in Budgets)
        {
            if (budget.GroupHeader) Console.WriteLine($"{budget.Category?.Name}");
            else Console.WriteLine($"\t{budget.Category?.Name} {budget.PercentageUsed:P0} used, {budget.Spent} out of {budget.LimitAmount}");
        }
    }
}