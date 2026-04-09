using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Clario.Models;
using Clario.Models.GeneralModels;
using Clario.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Clario.Messages;
using CommunityToolkit.Mvvm.Messaging;
using Supabase.Postgrest;
using Supabase.Realtime.PostgresChanges;
using Constants = Supabase.Realtime.Constants;
using FileOptions = Supabase.Storage.FileOptions;

namespace Clario.Data;

public record ProfileUpdated();

public partial class GeneralDataRepo : ObservableObject
{
    [ObservableProperty] private Profile? _profile;
    [ObservableProperty] private ObservableCollection<Category> _categories = new();
    [ObservableProperty] private ObservableCollection<Account> _accounts = new();
    [ObservableProperty] private ObservableCollection<Budget> _budgets = new();
    [ObservableProperty] private ObservableCollection<Transaction> _transactions = new();

    private static readonly HttpClient _HttpClient = new();
    private const string Bucket = "avatars";
    private const string ProjectRef = "xzxstbllaivumhtpctmo";
    private const string PublicBaseUrl = $"https://{ProjectRef}.supabase.co/storage/v1/object/public/{Bucket}";

    partial void OnProfileChanged(Profile? value)
    {
        _ = GetAvatarFromUrl(value?.AvatarUrl);
    }

    public async Task<Profile?> FetchProfileInfo(bool forceRefresh = false)
    {
        if (Profile is not null && !forceRefresh) return Profile;
        var profile = await SupabaseService.Client.From<Profile>().Get();
        if (profile.Models.Count == 0) return null;
        Profile = profile.Model;

        return Profile;
    }

    private async Task GetAvatarFromUrl(string? url)
    {
        if (!string.IsNullOrEmpty(url))
        {
            var bytes = await _HttpClient.GetByteArrayAsync(url);
            var stream = new MemoryStream(bytes);
            Profile.Avatar = new Bitmap(stream);
        }

        WeakReferenceMessenger.Default.Send(new ProfileUpdated());
    }


    public async Task<List<Transaction>> FetchTransactions(bool forceRefresh = false)
    {
        if (Transactions.Count != 0 && !forceRefresh) return Transactions.ToList();
        var transactions = await SupabaseService.Client.From<Transaction>().Get();
        Transactions = new ObservableCollection<Transaction>(transactions.Models);
        return transactions.Models;
    }

    public async Task InsertTransaction(Transaction transaction)
    {
        try
        {
            var result = await SupabaseService.Client.From<Transaction>().Insert(transaction);

            if (result.Models.Count >= 1)
            {
                var resultItem = LinkTransactionCategories(result.Models[0]);
                LinkTransactionAccounts(resultItem);
                Transactions.Add(resultItem);
            }
        }
        catch (Exception e)
        {
            DebugLogger.Log(e);
            return;
        }
    }

    public async Task UpdateTransaction(Transaction transaction)
    {
        try
        {
            var result = await SupabaseService.Client.From<Transaction>().Update(transaction);
            if (result.Model is null) return;
            var item = Transactions.FirstOrDefault(x => x.Id == result.Model.Id);
            if (item is null) return;
            var index = Transactions.IndexOf(item);

            if (index != -1)
            {
                var enriched = LinkTransactionCategories(result.Model);
                LinkTransactionAccounts(enriched);
                Transactions[index] = enriched;
            }
        }
        catch (Exception e)
        {
            DebugLogger.Log(e);
        }
    }

    public async Task DeleteTransaction(Guid id)
    {
        try
        {
            await SupabaseService.Client.From<Transaction>().Where(x => x.Id == id).Delete();
            var item = Transactions.FirstOrDefault(x => x.Id == id);
            if (item is null) return;
            Transactions.Remove(item);
        }
        catch (Exception e)
        {
            DebugLogger.Log(e);
            throw;
        }
    }

    public async Task InsertTransfer(Guid fromAccountId, Guid toAccountId, decimal amount, DateTime date, string? note)
    {
        try
        {
            var userId = Guid.Parse(SupabaseService.Client.Auth.CurrentUser!.Id!);
            var pairId = Guid.NewGuid();

            var fromCurrency = Accounts.FirstOrDefault(a => a.Id == fromAccountId)?.Currency ?? "";
            var toCurrency = Accounts.FirstOrDefault(a => a.Id == toAccountId)?.Currency ?? "";
            var toAmount = ConvertAmount(amount, fromCurrency, toCurrency);

            var outTx = new Transaction
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                AccountId = fromAccountId,
                Type = "transfer_out",
                Amount = amount,
                Description = "Transfer",
                Note = note?.Trim(),
                Date = date,
                TransferPairId = pairId,
            };
            var inTx = new Transaction
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                AccountId = toAccountId,
                Type = "transfer_in",
                Amount = toAmount,
                Description = "Transfer",
                Note = note?.Trim(),
                Date = date,
                TransferPairId = pairId,
            };

            var outResult = await SupabaseService.Client.From<Transaction>().Insert(outTx);
            var inResult = await SupabaseService.Client.From<Transaction>().Insert(inTx);

            if (outResult.Models.Count >= 1)
            {
                var enriched = LinkTransactionCategories(outResult.Models[0]);
                LinkTransactionAccounts(enriched);
                Transactions.Add(enriched);
            }

            if (inResult.Models.Count >= 1)
            {
                var enriched = LinkTransactionCategories(inResult.Models[0]);
                LinkTransactionAccounts(enriched);
                Transactions.Add(enriched);
            }

            // Re-enrich both so AccountDisplayText can reference the counterpart (from/to)
            LinkTransactionAccounts();
        }
        catch (Exception e)
        {
            DebugLogger.Log(e);
            throw;
        }
    }

    public async Task UpdateTransfer(Guid transferPairId, Guid fromAccountId, Guid toAccountId, decimal amount, DateTime date, string? note)
    {
        try
        {
            var pair = Transactions.Where(t => t.TransferPairId == transferPairId).ToList();
            var fromCurrency = Accounts.FirstOrDefault(a => a.Id == fromAccountId)?.Currency ?? "";
            var toCurrency = Accounts.FirstOrDefault(a => a.Id == toAccountId)?.Currency ?? "";
            var toAmount = ConvertAmount(amount, fromCurrency, toCurrency);

            foreach (var tx in pair)
            {
                tx.AccountId = tx.Type == "transfer_out" ? fromAccountId : toAccountId;
                tx.Amount = tx.Type == "transfer_in" ? toAmount : amount;
                tx.Date = date;
                tx.Note = note?.Trim();
                var result = await SupabaseService.Client.From<Transaction>().Update(tx);
                if (result.Model is null) continue;
                var index = Transactions.IndexOf(tx);
                if (index != -1)
                {
                    var enriched = LinkTransactionCategories(result.Model);
                    LinkTransactionAccounts(enriched);
                    Transactions[index] = enriched;
                }
            }

            LinkTransactionAccounts();
        }
        catch (Exception e)
        {
            DebugLogger.Log(e);
            throw;
        }
    }

    public async Task DeleteTransfer(Guid transferPairId)
    {
        try
        {
            await SupabaseService.Client.From<Transaction>()
                .Where(x => x.TransferPairId == transferPairId)
                .Delete();
            var pair = Transactions.Where(t => t.TransferPairId == transferPairId).ToList();
            foreach (var tx in pair)
                Transactions.Remove(tx);
        }
        catch (Exception e)
        {
            DebugLogger.Log(e);
            throw;
        }
    }

    public async Task<List<Category>> FetchCategories(bool forceRefresh = false)
    {
        if (Categories.Count != 0 && !forceRefresh) return Categories.ToList();

        var categories = await SupabaseService.Client.From<Category>().Get();
        Categories = new ObservableCollection<Category>(categories.Models);
        return categories.Models;
    }

    public async Task<Category?> InsertCategory(Category category)
    {
        try
        {
            var result = await SupabaseService.Client.From<Category>()
                .Insert(category, new QueryOptions() { Returning = QueryOptions.ReturnType.Representation });
            if (result.Model is null) return null;
            Categories.Add(result.Model);
            return result.Model;
        }
        catch (Exception e)
        {
            DebugLogger.Log(e);
            return null;
        }
    }

    public async Task UpdateCategory(Category category)
    {
        try
        {
            var result = await SupabaseService.Client.From<Category>().Update(category);
            if (result.Model is null) return;
            var item = Categories.FirstOrDefault(x => x.Id == result.Model.Id);
            if (item is null) return;
            var index = Categories.IndexOf(item);
            if (index != -1) Categories[index] = result.Model;
        }
        catch (Exception e)
        {
            DebugLogger.Log(e);
        }
    }

    public async Task DeleteCategory(Guid id)
    {
        try
        {
            await SupabaseService.Client.From<Category>().Where(x => x.Id == id).Delete();
            var item = Categories.FirstOrDefault(x => x.Id == id);
            if (item is null) return;
            Categories.Remove(item);
        }
        catch (Exception e)
        {
            DebugLogger.Log(e);
            throw;
        }
    }

    public async Task<List<Account>> FetchAccounts(bool forceRefresh = false)
    {
        if (Accounts.Count != 0 && !forceRefresh) return Accounts.ToList();
        var accounts = await SupabaseService.Client.From<Account>().Get();
        Accounts = new ObservableCollection<Account>(accounts.Models);
        return accounts.Models.OrderBy(x => x.IsPrimary).ThenBy(x => x.CreatedAt).ToList();
    }

    public async Task<List<Budget>> FetchBudgets(bool forceRefresh = false)
    {
        if (Budgets.Count != 0 && !forceRefresh) return Budgets.ToList();
        var budgets = await SupabaseService.Client.From<Budget>().Get();
        Budgets = new ObservableCollection<Budget>(budgets.Models);
        return budgets.Models;
    }

    public async Task<List<Budget>> FetchProcessedBudgets(DateTime CurrentPeriod)
    {
        var budgets = Budgets;
        var outputList = new List<Budget>();
        var primarySymbol = CurrencyService.GetSymbol(PrimaryAccount?.Currency ?? Profile?.Currency ?? "USD");
        foreach (var budget in budgets)
        {
            budget.Category = Categories.FirstOrDefault(x => x.Id == budget.CategoryId);
            budget.PrimarySymbol = primarySymbol;

            switch (budget.Period.ToLower())
            {
                case "monthly":
                    var budgetTransactions = Transactions.Where(x =>
                        x.Date.Month == CurrentPeriod.Month && x.Date.Year == CurrentPeriod.Year && x.CategoryId == budget.CategoryId).ToList();
                    budget.Spent = budgetTransactions.Sum(x => x.ConvertedAmount);
                    budget.TransactionsCount = budgetTransactions.Count;
                    break;
                case "quarterly":
                    var quarterTransactions = Transactions.Where(x =>
                        x.Date.Month >= CurrentPeriod.Month - 3 && x.Date.Month <= CurrentPeriod.Month && x.CategoryId == budget.CategoryId).ToList();
                    budget.Spent = quarterTransactions.Sum(x => x.ConvertedAmount);
                    budget.TransactionsCount = quarterTransactions.Count;
                    break;
                case "yearly":
                    var yearTransactions = Transactions.Where(x => x.Date.Year == CurrentPeriod.Year && x.CategoryId == budget.CategoryId).ToList();
                    budget.Spent = yearTransactions.Sum(x => x.ConvertedAmount);
                    budget.TransactionsCount = yearTransactions.Count;
                    break;
            }

            OnPropertyChanged(nameof(budget.IsOnTrack));
            OnPropertyChanged(nameof(budget.IsWarning));
            OnPropertyChanged(nameof(budget.IsOverBudget));
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

    public async Task<Account?> InsertAccount(Account account)
    {
        try
        {
            var result = await SupabaseService.Client.From<Account>()
                .Insert(account, new QueryOptions() { Returning = QueryOptions.ReturnType.Representation });
            if (result.Model is null) return null;
            Accounts.Add(result.Model);
            return result.Model;
        }
        catch (Exception e)
        {
            DebugLogger.Log(e);
            return null;
        }
    }

    public async Task UpdateAccount(Account account)
    {
        try
        {
            var result = await SupabaseService.Client.From<Account>().Update(account);
            if (result.Model is null) return;
            var item = Accounts.FirstOrDefault(x => x.Id == result.Model.Id);
            if (item is null) return;
            var index = Accounts.IndexOf(item);
            if (index != -1) Accounts[index] = result.Model;
        }
        catch (Exception e)
        {
            DebugLogger.Log(e);
        }
    }


    public async Task MigrateTransactions(Guid accountId, Guid targetAccountId)
    {
        try
        {
            var update = await SupabaseService.Client
                .From<Transaction>()
                .Where(x => x.AccountId == accountId)
                .Set(x => x.AccountId, targetAccountId)
                .Update();
            foreach (var updateModel in update.Models)
            {
                var item = Transactions.SingleOrDefault(x => x.Id == updateModel.Id);
                if (item is null) return;
                var index = Transactions.IndexOf(item);
                if (index != -1) Transactions[index] = updateModel;
            }
        }
        catch (Exception e)
        {
            DebugLogger.Log(e);
            throw;
        }
    }

    public async Task RecalculateAccountBalance(Guid targetAccountId)
    {
        var accountResult = Accounts
            .SingleOrDefault(a => a.Id == targetAccountId);

        if (accountResult is null) return;

        var transactionsResult = Transactions
            .Where(t => t.AccountId == targetAccountId);

        var balance = accountResult.OpeningBalance +
                      transactionsResult.Sum(t =>
                          t.Type is "income" or "transfer_in" ? t.Amount : -t.Amount);

        accountResult.CurrentBalance = balance;
        await SupabaseService.Client
            .From<Account>()
            .Update(accountResult);
        var index = Accounts.IndexOf(accountResult);
        if (index != -1) Accounts[index] = accountResult;
    }

    public async Task DeleteAccount(Guid accountId)
    {
        await SupabaseService.Client
            .From<Account>()
            .Where(a => a.Id == accountId)
            .Delete();

        var item = Accounts.FirstOrDefault(x => x.Id == accountId);
        if (item is null) return;
        Accounts.Remove(item);
    }

    public async Task InsertBudget(Budget budget)
    {
        try
        {
            var result = await SupabaseService.Client.From<Budget>().Insert(budget);
            if (result.Models.Count >= 1)
            {
                Budgets.Add(result.Models[0]);
            }
        }
        catch (Exception e)
        {
            DebugLogger.Log(e);
            throw;
        }
    }

    public async Task UpdateBudget(Budget budget)
    {
        try
        {
            var result = await SupabaseService.Client.From<Budget>().Update(budget);
            if (result.Model is null) return;
            var item = Budgets.FirstOrDefault(x => x.Id == result.Model.Id);
            if (item is null) return;
            var index = Budgets.IndexOf(item);
            if (index != -1) Budgets[index] = result.Model;
        }
        catch (Exception e)
        {
            DebugLogger.Log(e);
            throw;
        }
    }

    public async Task DeleteBudget(Guid BudgetId)
    {
        try
        {
            await SupabaseService.Client.From<Budget>().Where(x => x.Id == BudgetId).Delete();
            var item = Budgets.FirstOrDefault(x => x.Id == BudgetId);
            if (item is null) return;
            Budgets.Remove(item);
        }
        catch (Exception e)
        {
            DebugLogger.Log(e);
            throw;
        }
    }

    public Account? PrimaryAccount => Accounts.FirstOrDefault(a => a.IsPrimary);

    /// <summary>
    /// Clears is_primary on the current primary account (if different from <paramref name="newPrimaryId"/>).
    /// The caller must still save the new primary account via InsertAccount or UpdateAccount.
    /// </summary>
    public async Task SetPrimaryAccountAsync(Guid newPrimaryId)
    {
        try
        {
            var old = Accounts.FirstOrDefault(a => a.IsPrimary && a.Id != newPrimaryId);
            if (old is null) return;
            old.IsPrimary = false;
            var result = await SupabaseService.Client.From<Account>().Update(old);
            if (result.Model is null) return;
            var idx = Accounts.IndexOf(old);
            if (idx != -1) Accounts[idx] = result.Model;
        }
        catch (Exception e)
        {
            DebugLogger.Log(e);
        }
    }

    public async Task RefreshLiveRatesAndEnrich()
    {
        var primaryCurrency = PrimaryAccount?.Currency ?? Profile?.Currency ?? "USD";
        await CurrencyService.RefreshLiveRatesAsync(primaryCurrency, Accounts.Select(a => a.Currency));
        LinkTransactionAccounts();
        WeakReferenceMessenger.Default.Send(new RatesRefreshed());
    }

    /// Converts <paramref name="amount"/> from <paramref name="fromCurrency"/> to
    /// <paramref name="toCurrency"/> using the current live rates.
    /// Falls back to <paramref name="amount"/> unchanged when currencies match or rates are missing.
    private static decimal ConvertAmount(decimal amount, string fromCurrency, string toCurrency)
    {
        if (string.IsNullOrEmpty(fromCurrency) || string.IsNullOrEmpty(toCurrency)) return amount;
        if (fromCurrency.Equals(toCurrency, StringComparison.OrdinalIgnoreCase)) return amount;
        if (!CurrencyService.LiveRates.TryGetValue(fromCurrency, out var fromRate)) return amount;
        if (!CurrencyService.LiveRates.TryGetValue(toCurrency, out var toRate) || toRate == 0) return amount;
        // fromRate = 1 fromCurrency in primary; toRate = 1 toCurrency in primary
        // amount * fromRate / toRate = amount converted to toCurrency
        return Math.Round(amount * fromRate / toRate, 6);
    }

    public void LinkTransactionCategories()
    {
        foreach (var transaction in Transactions)
        {
            transaction.Category = Categories.FirstOrDefault(x => x.Id == transaction.CategoryId);
        }
    }

    public Transaction LinkTransactionCategories(Transaction transaction)
    {
        transaction.Category = Categories.FirstOrDefault(x => x.Id == transaction.CategoryId);
        return transaction;
    }

    public void LinkTransactionAccounts()
    {
        var primaryCurrency = PrimaryAccount?.Currency ?? Profile?.Currency ?? "USD";
        var primarySymbol = CurrencyService.GetSymbol(primaryCurrency);
        foreach (var tx in Transactions)
        {
            EnrichTransactionAccount(tx, primaryCurrency, primarySymbol);
        }
    }

    public Transaction LinkTransactionAccounts(Transaction tx)
    {
        var primaryCurrency = PrimaryAccount?.Currency ?? Profile?.Currency ?? "USD";
        var primarySymbol = CurrencyService.GetSymbol(primaryCurrency);
        EnrichTransactionAccount(tx, primaryCurrency, primarySymbol);
        return tx;
    }

    private void EnrichTransactionAccount(Transaction tx, string primaryCurrency, string primarySymbol)
    {
        var account = Accounts.FirstOrDefault(a => a.Id == tx.AccountId);
        var accountCurrency = account?.Currency ?? primaryCurrency;
        tx.AccountCurrency = accountCurrency;
        tx.IsMultiCurrency = !accountCurrency.Equals(primaryCurrency, StringComparison.OrdinalIgnoreCase);
        tx.PrimaryAmountFormatted = $"{primarySymbol}{tx.ConvertedAmount:N2}";
        tx.OriginalAmountFormatted = tx.IsMultiCurrency
            ? $"{CurrencyService.GetSymbol(accountCurrency)}{tx.Amount:N2}"
            : string.Empty;

        if (tx.IsTransfer && tx.TransferPairId.HasValue)
        {
            var counterpart = Transactions.FirstOrDefault(t => t.TransferPairId == tx.TransferPairId && t.Id != tx.Id);
            var counterpartAccount = counterpart is not null ? Accounts.FirstOrDefault(a => a.Id == counterpart.AccountId) : null;
            var fromName = tx.IsTransferOut ? (account?.Name ?? "?") : (counterpartAccount?.Name ?? "?");
            var toName = tx.IsTransferOut ? (counterpartAccount?.Name ?? "?") : (account?.Name ?? "?");
            tx.AccountDisplayText = $"{fromName} → {toName}";
        }
        else
        {
            tx.AccountDisplayText = account?.Name ?? "";
        }
    }

    public async Task UpdateSavingsGoal(decimal? goal)
    {
        var profile = Profile;
        profile.SavingsGoal = goal;
        var result = await SupabaseService.Client.From<Profile>().Update(profile);
        if (result.Models.Count < 1) return;
        Profile = result.Models[0];
    }

    public async Task UpdateProfile(Profile profile)
    {
        var result = await SupabaseService.Client.From<Profile>().Update(profile);
        if (result.Models.Count > 0) Profile = result.Models[0];
    }

    public async Task UpdateProfileAvatar(string? avatarUrl)
    {
        var profile = Profile;
        profile.AvatarUrl = avatarUrl;

        var result = await SupabaseService.Client
            .From<Profile>()
            .Update(profile);
        Profile = result.Models[0];
    }


    /// <summary>Upload a local file as the current user's avatar. Returns the public URL.</summary>
    public async Task<string> UploadAvatarAsync(string localFilePath)
    {
        var userId = SupabaseService.Client.Auth.CurrentUser!.Id;
        var ext = Path.GetExtension(localFilePath).ToLowerInvariant();
        var storagePath = $"{userId}/avatar{ext}";

        var bytes = await File.ReadAllBytesAsync(localFilePath);
        var mimeType = ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };

        var bucket = SupabaseService.Client.Storage.From(Bucket);

        // Upsert: upload if not exists, replace if it does
        await bucket.Upload(bytes, storagePath, new FileOptions
        {
            ContentType = mimeType,
            Upsert = true
        });

        var stream = new MemoryStream(bytes);
        // Append cache-buster so Avalonia Image re-fetches the new file
        return $"{PublicBaseUrl}/{storagePath}?t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
    }

    /// <summary>Delete the current user's avatar from storage.</summary>
    public async Task DeleteAvatarAsync()
    {
        var userId = SupabaseService.Client.Auth.CurrentUser!.Id;

        // Try both extensions since we don't track which was uploaded
        var bucket = SupabaseService.Client.Storage.From(Bucket);
        foreach (var ext in new[] { "jpg", "jpeg", "png", "webp" })
        {
            try
            {
                await bucket.Remove([$"{userId}/avatar.{ext}"]);
            }
            catch
            {
                /* file with that ext may not exist, ignore */
            }
        }
    }

    /// <summary>Build the public URL for a given avatar_url stored in the profile.</summary>
    public string? BuildPublicUrl(string? avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl)) return null;
        // If already a full URL (from storage or external), return as-is
        if (avatarUrl.StartsWith("http")) return avatarUrl;
        return $"{PublicBaseUrl}/{avatarUrl}";
    }

    public void StartRealtimeSync()
    {
        if (SupabaseService.Client.Auth.CurrentUser?.Id is null) return;
        DebugLogger.Log("[Realtime] StartRealtimeSync: registering listeners");

        //  Transactions 
        _ = SupabaseService.Client.From<Transaction>().On(PostgresChangesOptions.ListenType.Inserts, (_, c) =>
        {
            var insertedTransaction = c.Model<Transaction>();
            if (insertedTransaction is null) { DebugLogger.Log("[Realtime] Transaction INSERT: model was null"); return; }
            DebugLogger.Log($"[Realtime] Transaction INSERT: {insertedTransaction.Id} ({insertedTransaction.Description})");
            Dispatcher.UIThread.Post(() =>
            {
                if (Transactions.Any(x => x.Id == insertedTransaction.Id)) { DebugLogger.Log($"[Realtime] Transaction INSERT: skipped duplicate {insertedTransaction.Id}"); return; }
                LinkTransactionCategories(insertedTransaction);
                LinkTransactionAccounts(insertedTransaction);
                Transactions.Add(insertedTransaction);
                DebugLogger.Log($"[Realtime] Transaction INSERT: added to collection");
            });
        });

        _ = SupabaseService.Client.From<Transaction>().On(PostgresChangesOptions.ListenType.Updates, (_, c) =>
        {
            var updatedTransaction = c.Model<Transaction>();
            if (updatedTransaction is null) { DebugLogger.Log("[Realtime] Transaction UPDATE: model was null"); return; }
            DebugLogger.Log($"[Realtime] Transaction UPDATE: {updatedTransaction.Id} ({updatedTransaction.Description})");
            Dispatcher.UIThread.Post(() =>
            {
                var idx = Transactions.ToList().FindIndex(x => x.Id == updatedTransaction.Id);
                if (idx == -1) { DebugLogger.Log($"[Realtime] Transaction UPDATE: id {updatedTransaction.Id} not found in collection"); return; }
                LinkTransactionCategories(updatedTransaction);
                LinkTransactionAccounts(updatedTransaction);
                Transactions[idx] = updatedTransaction;
                DebugLogger.Log($"[Realtime] Transaction UPDATE: replaced at index {idx}");
            });
        });

        _ = SupabaseService.Client.From<Transaction>().On(PostgresChangesOptions.ListenType.Deletes, (_, c) =>
        {
            var deletedTransaction = c.OldModel<Transaction>();
            if (deletedTransaction is null) { DebugLogger.Log("[Realtime] Transaction DELETE: old model was null"); return; }
            DebugLogger.Log($"[Realtime] Transaction DELETE: {deletedTransaction.Id}");
            Dispatcher.UIThread.Post(() =>
            {
                var item = Transactions.FirstOrDefault(x => x.Id == deletedTransaction.Id);
                if (item is not null) { Transactions.Remove(item); DebugLogger.Log($"[Realtime] Transaction DELETE: removed {deletedTransaction.Id}"); }
                else DebugLogger.Log($"[Realtime] Transaction DELETE: id {deletedTransaction.Id} not found (already removed locally)");
            });
        });

        //  Accounts 
        _ = SupabaseService.Client.From<Account>().On(PostgresChangesOptions.ListenType.Inserts, (_, c) =>
        {
            var insertedAccount = c.Model<Account>();
            if (insertedAccount is null) { DebugLogger.Log("[Realtime] Account INSERT: model was null"); return; }
            DebugLogger.Log($"[Realtime] Account INSERT: {insertedAccount.Id} ({insertedAccount.Name})");
            Dispatcher.UIThread.Post(() =>
            {
                if (Accounts.Any(x => x.Id == insertedAccount.Id)) { DebugLogger.Log($"[Realtime] Account INSERT: skipped duplicate {insertedAccount.Id}"); return; }
                Accounts.Add(insertedAccount);
                DebugLogger.Log($"[Realtime] Account INSERT: added to collection");
            });
        });

        _ = SupabaseService.Client.From<Account>().On(PostgresChangesOptions.ListenType.Updates, (_, c) =>
        {
            var updatedAccount = c.Model<Account>();
            if (updatedAccount is null) { DebugLogger.Log("[Realtime] Account UPDATE: model was null"); return; }
            DebugLogger.Log($"[Realtime] Account UPDATE: {updatedAccount.Id} ({updatedAccount.Name})");
            Dispatcher.UIThread.Post(() =>
            {
                var idx = Accounts.ToList().FindIndex(x => x.Id == updatedAccount.Id);
                if (idx != -1) { Accounts[idx] = updatedAccount; DebugLogger.Log($"[Realtime] Account UPDATE: replaced at index {idx}"); }
                else DebugLogger.Log($"[Realtime] Account UPDATE: id {updatedAccount.Id} not found in collection");
            });
        });

        _ = SupabaseService.Client.From<Account>().On(PostgresChangesOptions.ListenType.Deletes, (_, c) =>
        {
            var deletedAccount = c.OldModel<Account>();
            if (deletedAccount is null) { DebugLogger.Log("[Realtime] Account DELETE: old model was null"); return; }
            DebugLogger.Log($"[Realtime] Account DELETE: {deletedAccount.Id}");
            Dispatcher.UIThread.Post(() =>
            {
                var item = Accounts.FirstOrDefault(x => x.Id == deletedAccount.Id);
                if (item is not null) { Accounts.Remove(item); DebugLogger.Log($"[Realtime] Account DELETE: removed {deletedAccount.Id}"); }
                else DebugLogger.Log($"[Realtime] Account DELETE: id {deletedAccount.Id} not found (already removed locally)");
            });
        });

        //  Budgets 
        _ = SupabaseService.Client.From<Budget>().On(PostgresChangesOptions.ListenType.Inserts, (_, c) =>
        {
            var insertedBudget = c.Model<Budget>();
            if (insertedBudget is null) { DebugLogger.Log("[Realtime] Budget INSERT: model was null"); return; }
            DebugLogger.Log($"[Realtime] Budget INSERT: {insertedBudget.Id}");
            Dispatcher.UIThread.Post(() =>
            {
                if (Budgets.Any(x => x.Id == insertedBudget.Id)) { DebugLogger.Log($"[Realtime] Budget INSERT: skipped duplicate {insertedBudget.Id}"); return; }
                Budgets.Add(insertedBudget);
                DebugLogger.Log($"[Realtime] Budget INSERT: added to collection");
            });
        });

        _ = SupabaseService.Client.From<Budget>().On(PostgresChangesOptions.ListenType.Updates, (_, c) =>
        {
            var updatedBudget = c.Model<Budget>();
            if (updatedBudget is null) { DebugLogger.Log("[Realtime] Budget UPDATE: model was null"); return; }
            DebugLogger.Log($"[Realtime] Budget UPDATE: {updatedBudget.Id}");
            Dispatcher.UIThread.Post(() =>
            {
                var idx = Budgets.ToList().FindIndex(x => x.Id == updatedBudget.Id);
                if (idx != -1) { Budgets[idx] = updatedBudget; DebugLogger.Log($"[Realtime] Budget UPDATE: replaced at index {idx}"); }
                else DebugLogger.Log($"[Realtime] Budget UPDATE: id {updatedBudget.Id} not found in collection");
            });
        });

        _ = SupabaseService.Client.From<Budget>().On(PostgresChangesOptions.ListenType.Deletes, (_, c) =>
        {
            var deletedBudget = c.OldModel<Budget>();
            if (deletedBudget is null) { DebugLogger.Log("[Realtime] Budget DELETE: old model was null"); return; }
            DebugLogger.Log($"[Realtime] Budget DELETE: {deletedBudget.Id}");
            Dispatcher.UIThread.Post(() =>
            {
                var item = Budgets.FirstOrDefault(x => x.Id == deletedBudget.Id);
                if (item is not null) { Budgets.Remove(item); DebugLogger.Log($"[Realtime] Budget DELETE: removed {deletedBudget.Id}"); }
                else DebugLogger.Log($"[Realtime] Budget DELETE: id {deletedBudget.Id} not found (already removed locally)");
            });
        });

        //  Categories 
        _ = SupabaseService.Client.From<Category>().On(PostgresChangesOptions.ListenType.Inserts, (_, c) =>
        {
            var insertedCategory = c.Model<Category>();
            if (insertedCategory is null) { DebugLogger.Log("[Realtime] Category INSERT: model was null"); return; }
            DebugLogger.Log($"[Realtime] Category INSERT: {insertedCategory.Id} ({insertedCategory.Name})");
            Dispatcher.UIThread.Post(() =>
            {
                if (Categories.Any(x => x.Id == insertedCategory.Id)) { DebugLogger.Log($"[Realtime] Category INSERT: skipped duplicate {insertedCategory.Id}"); return; }
                Categories.Add(insertedCategory);
                DebugLogger.Log($"[Realtime] Category INSERT: added to collection");
            });
        });

        _ = SupabaseService.Client.From<Category>().On(PostgresChangesOptions.ListenType.Updates, (_, c) =>
        {
            var UpdatedCategory = c.Model<Category>();
            if (UpdatedCategory is null) { DebugLogger.Log("[Realtime] Category UPDATE: model was null"); return; }
            DebugLogger.Log($"[Realtime] Category UPDATE: {UpdatedCategory.Id} ({UpdatedCategory.Name})");
            Dispatcher.UIThread.Post(() =>
            {
                var idx = Categories.ToList().FindIndex(x => x.Id == UpdatedCategory.Id);
                if (idx != -1) { Categories[idx] = UpdatedCategory; DebugLogger.Log($"[Realtime] Category UPDATE: replaced at index {idx}"); }
                else DebugLogger.Log($"[Realtime] Category UPDATE: id {UpdatedCategory.Id} not found in collection");
            });
        });

        _ = SupabaseService.Client.From<Category>().On(PostgresChangesOptions.ListenType.Deletes, (_, c) =>
        {
            var deletedCategory = c.OldModel<Category>();
            if (deletedCategory is null) { DebugLogger.Log("[Realtime] Category DELETE: old model was null"); return; }
            DebugLogger.Log($"[Realtime] Category DELETE: {deletedCategory.Id}");
            Dispatcher.UIThread.Post(() =>
            {
                var item = Categories.FirstOrDefault(x => x.Id == deletedCategory.Id);
                if (item is not null) { Categories.Remove(item); DebugLogger.Log($"[Realtime] Category DELETE: removed {deletedCategory.Id}"); }
                else DebugLogger.Log($"[Realtime] Category DELETE: id {deletedCategory.Id} not found (already removed locally)");
            });
        });

        //  Profile 
        _ = SupabaseService.Client.From<Profile>().On(PostgresChangesOptions.ListenType.Updates, (_, c) =>
        {
            var updatedProfile = c.Model<Profile>();
            if (updatedProfile is null) { DebugLogger.Log("[Realtime] Profile UPDATE: model was null"); return; }
            DebugLogger.Log($"[Realtime] Profile UPDATE: {updatedProfile.Id} ({updatedProfile.DisplayName})");
            Dispatcher.UIThread.Post(() => Profile = updatedProfile);
        });

        DebugLogger.Log("[Realtime] all listeners registered");
    }
}