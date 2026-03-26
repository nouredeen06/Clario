using System;
using System.Linq;
using Clario.Data;
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

    private Guid? _categoryId;

    [Column("category_id")]
    public Guid? CategoryId
    {
        get => _categoryId;
        set
        {
            _categoryId = value;

            Category = DataRepo.General.FetchCategories().Result.FirstOrDefault(x => x.Id == value);
        }
    }

    [JsonIgnore] public Category? Category { get; set; }

    [Column("amount")] public decimal Amount { get; set; }

    [Column("type")] public string Type { get; set; } = string.Empty; // "income" or "expense"

    [Column("description")] public string Description { get; set; } = string.Empty;

    [Column("note")] public string? Note { get; set; }

    [Column("date")] public DateTime Date { get; set; }

    [Column("created_at")] public DateTime CreatedAt { get; set; }

    [JsonIgnore] public bool GroupHeader { get; set; } = false;
}