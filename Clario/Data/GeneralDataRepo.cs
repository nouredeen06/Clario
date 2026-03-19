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

    public async Task<Profile?> FetchProfileInfo()
    {
        if (Profile is not null) return Profile;

        var profile = await SupabaseService.Client.From<Profile>().Get();
        return profile.Models.FirstOrDefault();
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
        try
        {
            var transactions = await SupabaseService.Client.From<Transaction>().Get();
            return transactions.Models;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task<List<Category>> FetchCategories()
    {
        try
        {
            if (Categories is not null) return Categories;
            var categories = await SupabaseService.Client.From<Category>().Get();
            Categories = categories.Models;
            // categories.Models.Select(x=>x.Icon).
            return categories.Models;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
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
        var budgets = await SupabaseService.Client.From<Budget>().Get();
        return budgets.Models;
    }
}