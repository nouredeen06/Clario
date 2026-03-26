using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Clario.Models;

[Table("categories")]
public class Category : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }

    [Column("user_id")] public Guid UserId { get; set; }

    [Column("name")] public string Name { get; set; } = string.Empty;

    [Column("icon")] public string Icon { get; set; } = string.Empty;

    [Column("color")] public string Color { get; set; } = string.Empty;

    [Column("type")] public string Type { get; set; } = string.Empty; // "income" or "expense"

    [Column("created_at")] public DateTime CreatedAt { get; set; }
}