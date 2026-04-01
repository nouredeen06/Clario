using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Clario.Models;

[Table("accounts")]
public class Account : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }

    [Column("user_id")] public Guid UserId { get; set; }

    [Column("name")] public string Name { get; set; } = string.Empty;

    [Column("type")] public string Type { get; set; } = string.Empty; // "checking", "savings", "credit", "cash", "investment"

    [Column("institution")] public string? Institution { get; set; }

    [Column("mask")] public string? Mask { get; set; }

    [Column("currency")] public string Currency { get; set; } = "USD";

    [Column("opening_balance")] public decimal OpeningBalance { get; set; }
    [JsonIgnore] public decimal CurrentBalance { get; set; }

    [Column("credit_limit")] public decimal? CreditLimit { get; set; }

    [Column("is_archived")] public bool IsArchived { get; set; }

    [Column("opened_at")] public DateTime? OpenedAt { get; set; }

    [Column("created_at")] public DateTime CreatedAt { get; set; }

    [Column("icon")] public string Icon { get; set; } = string.Empty;

    [Column("color")] public string Color { get; set; } = string.Empty;

    [JsonIgnore] public int TransactionsCount { get; set; }
    [JsonIgnore] public int IncomeTransactionsThisMonth { get; set; }
    [JsonIgnore] public int ExpenseTransactionsThisMonth { get; set; }
    [JsonIgnore] public decimal TotalIncomeThisMonth { get; set; }
    [JsonIgnore] public decimal TotalExpenseThisMonth { get; set; }
    [JsonIgnore] public decimal MonthlyIncrease { get; set; }
    [JsonIgnore] public List<Transaction>? RecentTransactions { get; set; }
    [JsonIgnore] public bool isCredit => Type == "Credit";
    [JsonIgnore] public decimal CreditUtilizationPerc => (CurrentBalance < 0 ? CurrentBalance * -1 : 0) / (CreditLimit == 0 ? 1 : CreditLimit) ?? 1;
    [JsonIgnore] public bool GroupHeader { get; set; } = false;
}