using System;
using Avalonia.Media.Imaging;
using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Clario.Models.GeneralModels;

[Table("profiles")]
public class Profile : BaseModel
{
    [PrimaryKey("id", false)] public Guid Id { get; set; }
    [Column("display_name")] public string DisplayName { get; set; }
    [Column("avatar_url")] public string? AvatarUrl { get; set; }
    [JsonIgnore] public Bitmap? Avatar { get; set; }
    [JsonIgnore] public bool HasAvatar => !string.IsNullOrWhiteSpace(AvatarUrl);
    [Column("currency")] public string Currency { get; set; }
    [Column("theme")] public string Theme { get; set; }
    [Column("language")] public string Language { get; set; }
    [Column("savings_goal")] public decimal? SavingsGoal { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }

    public Profile(string displayName,
        string? avatarUrl,
        string currency = "usd",
        string? theme = "system",
        string? language = "en-us")
    {
        DisplayName = displayName;
        AvatarUrl = avatarUrl;
        Currency = currency;
        Theme = theme ?? "system";
        Language = language ?? "en-us";
        CreatedAt = DateTime.Now;
        UpdatedAt = DateTime.Now;
    }
    
    public Profile()
    {
        CreatedAt = DateTime.Now;
        UpdatedAt = DateTime.Now;
    }
}