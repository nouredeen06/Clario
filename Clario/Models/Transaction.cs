using System;
using System.Linq;
using Clario.Data;
using Clario.Services;
using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Clario.Models;

[Table("transactions")]
public class Transaction : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }

    [Column("user_id")] public Guid UserId { get; set; }

    [Column("account_id")] public Guid AccountId { get; set; }

    [Column("category_id")] public Guid? CategoryId { get; set; }

    [JsonIgnore] public Category? Category { get; set; }

    [Column("amount")] public decimal Amount { get; set; }

    [Column("type")] public string Type { get; set; } = string.Empty; // "income" or "expense"

    [Column("description")] public string Description { get; set; } = string.Empty;

    [Column("note")] public string? Note { get; set; }

    [Column("date")] public DateTime Date { get; set; }

    [Column("created_at")] public DateTime CreatedAt { get; set; }

    [Column("exchange_rate")] public decimal? ExchangeRate { get; set; }

    // Set during enrichment by GeneralDataRepo.LinkTransactionAccounts
    [JsonIgnore] public string AccountCurrency { get; set; } = "";
    [JsonIgnore] public string PrimaryAmountFormatted { get; set; } = "";
    [JsonIgnore] public string OriginalAmountFormatted { get; set; } = "";

    [JsonIgnore] public decimal ConvertedAmount =>
        !string.IsNullOrEmpty(AccountCurrency) && CurrencyService.LiveRates.TryGetValue(AccountCurrency, out var liveRate)
            ? Amount * liveRate
            : (ExchangeRate.HasValue ? Amount * ExchangeRate.Value : Amount);
    [JsonIgnore] public bool IsMultiCurrency { get; set; }
    [JsonIgnore] public string PrimaryAmountSignFormatted =>
        Type == "expense" ? $"-{PrimaryAmountFormatted}" : $"+{PrimaryAmountFormatted}";

    [JsonIgnore] public bool GroupHeader { get; set; } = false;
}