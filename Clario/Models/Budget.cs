using System;
using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;


namespace Clario.Models;

[Table("budgets")]
public class Budget : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }

    [Column("user_id")] public Guid UserId { get; set; }

    [Column("category_id")] public Guid CategoryId { get; set; }

    [Column("amount")] public decimal LimitAmount { get; set; }

    [Column("period")] public string Period { get; set; } = "monthly";

    [Column("alert_threshold")] public int AlertThreshold { get; set; } = 80;

    [Column("rollover")] public bool Rollover { get; set; }

    [Column("created_at")] public DateTime CreatedAt { get; set; }

    // ── not in DB ──────────────────────────────────────

    [JsonIgnore] public Category? Category { get; set; }
    [JsonIgnore] public int TransactionsCount { get; set; }
    [JsonIgnore] public decimal Spent { get; set; }
    [JsonIgnore] public string PrimarySymbol { get; set; } = "$";

    [JsonIgnore] public decimal Remaining => LimitAmount - Spent;
    [JsonIgnore] public double PercentageUsed => LimitAmount > 0 ? Math.Round((double)(Spent / LimitAmount), 2) : 0;
    [JsonIgnore] public bool IsOverBudget => Spent > LimitAmount;
    [JsonIgnore] public bool IsWarning => !IsOverBudget && PercentageUsed * 100 >= AlertThreshold;
    [JsonIgnore] public bool IsOnTrack => PercentageUsed * 100 < AlertThreshold;

    [JsonIgnore] public string SpentFormatted => $"{PrimarySymbol}{Spent:N0}";
    [JsonIgnore] public string LimitFormatted => $"{PrimarySymbol}{LimitAmount:N0}";
    [JsonIgnore] public string AmountFormatted => $"of {PrimarySymbol}{LimitAmount:N0}";
    [JsonIgnore] public string PercentageFormatted => $"{PercentageUsed:P0} used";

    [JsonIgnore]
    public string RemainingFormatted => IsOverBudget
        ? $"{PrimarySymbol}{Math.Abs(Remaining):N0} over"
        : $"{PrimarySymbol}{Remaining:N0} left";

    [JsonIgnore] public bool GroupHeader { get; set; } = false;
}