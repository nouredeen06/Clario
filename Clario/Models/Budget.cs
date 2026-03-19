using System;
using Clario.Models;
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

    public Category? Category { get; set; }
    public int TransactionsCount { get; set; }
    public decimal Spent { get; set; } // populated after joining with transactions

    public decimal Remaining => LimitAmount - Spent;
    public double PercentageUsed => LimitAmount > 0 ? Math.Round((double)(Spent / LimitAmount), 2) : 0;
    public bool IsOverBudget => Spent > LimitAmount;
    public bool IsWarning => !IsOverBudget && PercentageUsed * 100 >= AlertThreshold;
    public bool IsOnTrack => !IsOverBudget && PercentageUsed * 100 < AlertThreshold;

    public string SpentFormatted => $"${Spent:N0}";
    public string AmountFormatted => $"of ${LimitAmount:N0}";
    public string PercentageFormatted => $"{PercentageUsed:P0} used";

    public string RemainingFormatted => IsOverBudget
        ? $"${Math.Abs(Remaining):N0} over"
        : $"${Remaining:N0} left";

    public bool GroupHeader { get; set; } = false;
}