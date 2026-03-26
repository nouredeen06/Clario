using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Clario.Models;
using Clario.Models.GeneralModels;
using Clario.Services;

namespace Clario.Data;

public class GeneralDataRepo
{
    public Profile? Profile { get; set; }
    public List<Category>? Categories { get; set; }
    public List<Account>? Accounts { get; set; }
    public List<Budget>? Budgets { get; set; }
    public List<Transaction>? Transactions { get; set; }

    public async Task<Profile?> FetchProfileInfo()
    {
        if (Profile is not null) return Profile;
        var profile = await SupabaseService.Client.From<Profile>().Get();
        Profile = profile.Model;
        return profile.Model;
    }

    public async Task InsertProfileInfo(Profile profile)
    {
        try
        {
            await SupabaseService.Client.From<Profile>().Insert(profile);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return;
        }

        Profile = profile;
    }

    public async Task<List<Transaction>> FetchTransactions()
    {
        if (Transactions is not null) return Transactions;
        var transactions = await SupabaseService.Client.From<Transaction>().Get();
        Transactions = transactions.Models;
        return transactions.Models;
    }

    public async Task InsertTransaction(Transaction transaction)
    {
        try
        {
            await SupabaseService.Client.From<Transaction>().Insert(transaction);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async Task UpdateTransaction(Transaction transaction)
    {
        try
        {
            await SupabaseService.Client.From<Transaction>().Update(transaction);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async Task DeleteTransaction(Guid id)
    {
        try
        {
            await SupabaseService.Client.From<Transaction>().Where(x => x.Id == id).Delete();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task<List<Category>> FetchCategories()
    {
        if (Categories is not null) return Categories;
        var categories = await SupabaseService.Client.From<Category>().Get();
        Categories = categories.Models;
        return categories.Models;
    }

    public async Task<List<Account>> FetchAccounts()
    {
        if (Accounts is not null) return Accounts;
        var accounts = await SupabaseService.Client.From<Account>().Get();
        Accounts = accounts.Models;
        return accounts.Models;
    }

    public async Task<List<Budget>> FetchBudgets()
    {
        if (Budgets is not null) return Budgets;
        var budgets = await SupabaseService.Client.From<Budget>().Get();
        Budgets = budgets.Models;
        return budgets.Models;
    }

    public async Task<List<Budget>> FetchProcessedBudgets(DateTime CurrentPeriod)
    {
        var categories = await FetchCategories();
        var transactions = await FetchTransactions();
        var budgets = await FetchBudgets();
        var outputList = new List<Budget>();
        foreach (var budget in budgets)
        {
            budget.Category = categories.FirstOrDefault(x => x.Id == budget.CategoryId);

            switch (budget.Period.ToLower())
            {
                case "monthly":
                    var budgetTransactions = transactions.Where(x =>
                        x.Date.Month == CurrentPeriod.Month && x.Date.Year == CurrentPeriod.Year && x.CategoryId == budget.CategoryId).ToList();
                    budget.Spent = budgetTransactions.Sum(x => x.Amount);
                    budget.TransactionsCount = budgetTransactions.Count;
                    break;
                case "quarterly":
                    var quarterTransactions = transactions.Where(x =>
                        x.Date.Month >= CurrentPeriod.Month - 3 && x.Date.Month <= CurrentPeriod.Month && x.CategoryId == budget.CategoryId).ToList();
                    budget.Spent = quarterTransactions.Sum(x => x.Amount);
                    budget.TransactionsCount = quarterTransactions.Count;
                    break;
                case "yearly":
                    var yearTransactions = transactions.Where(x => x.Date.Year == CurrentPeriod.Year && x.CategoryId == budget.CategoryId).ToList();
                    budget.Spent = yearTransactions.Sum(x => x.Amount);
                    budget.TransactionsCount = yearTransactions.Count;
                    break;
            }
        }


        if (budgets.Any(x => x.IsOnTrack))
        {
            outputList.Add(new Budget() { Category = new Category() { Name = "ON TRACK" }, GroupHeader = true });
            var onTrack = budgets.Where(x => x.IsOnTrack).OrderByDescending(x => x.PercentageUsed).ToList();
            foreach (var budget in onTrack)
            {
                outputList.Add(budget);
            }
        }

        if (budgets.Any(x => x.IsWarning))
        {
            outputList.Add(new Budget() { Category = new Category() { Name = "APPROACHING LIMIT" }, GroupHeader = true });
            var approaching = budgets.Where(x => x.IsWarning).OrderByDescending(x => x.PercentageUsed).ToList();
            foreach (var budget in approaching)
            {
                outputList.Add(budget);
            }
        }

        if (budgets.Any(x => x.IsOverBudget))
        {
            outputList.Add(new Budget() { Category = new Category() { Name = "OVER BUDGET" }, GroupHeader = true });
            var overBudget = budgets.Where(x => x.IsOverBudget).OrderByDescending(x => x.PercentageUsed).ToList();
            foreach (var budget in overBudget)
            {
                outputList.Add(budget);
            }
        }

        return outputList;
    }
}