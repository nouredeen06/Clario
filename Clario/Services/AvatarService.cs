// using System;
// using System.IO;
// using System.Threading.Tasks;
// using Avalonia.Media.Imaging;
// using Clario.Data;
// using Supabase.Storage;
// using FileOptions = Supabase.Storage.FileOptions;
//
// namespace Clario.Services;
//
// public class AvatarService
// {
//     public static AvatarService Instance = new();
//
//     private const string Bucket = "avatars";
//     private const string ProjectRef = "xzxstbllaivumhtpctmo";
//     private const string PublicBaseUrl = $"https://{ProjectRef}.supabase.co/storage/v1/object/public/{Bucket}";
//
//     /// <summary>Upload a local file as the current user's avatar. Returns the public URL.</summary>
//     public async Task<string> UploadAvatarAsync(string localFilePath)
//     {
//         var userId = SupabaseService.Client.Auth.CurrentUser!.Id;
//         var ext = Path.GetExtension(localFilePath).ToLowerInvariant();
//         var storagePath = $"{userId}/avatar{ext}";
//
//         var bytes = await File.ReadAllBytesAsync(localFilePath);
//         var mimeType = ext switch
//         {
//             ".jpg" or ".jpeg" => "image/jpeg",
//             ".png" => "image/png",
//             ".webp" => "image/webp",
//             _ => "application/octet-stream"
//         };
//
//         var bucket = SupabaseService.Client.Storage.From(Bucket);
//
//         // Upsert: upload if not exists, replace if it does
//         await bucket.Upload(bytes, storagePath, new FileOptions
//         {
//             ContentType = mimeType,
//             Upsert = true
//         });
//
//         var stream = new MemoryStream(bytes);
//         DataRepo.General.Profile!.Avatar = new Bitmap(stream);
//         // Append cache-buster so Avalonia Image re-fetches the new file
//         return $"{PublicBaseUrl}/{storagePath}?t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
//     }
//
//     /// <summary>Delete the current user's avatar from storage.</summary>
//     public async Task DeleteAvatarAsync()
//     {
//         var userId = SupabaseService.Client.Auth.CurrentUser!.Id;
//
//         // Try both extensions since we don't track which was uploaded
//         var bucket = SupabaseService.Client.Storage.From(Bucket);
//         foreach (var ext in new[] { "jpg", "jpeg", "png", "webp" })
//         {
//             try
//             {
//                 await bucket.Remove([$"{userId}/avatar.{ext}"]);
//             }
//             catch
//             {
//                 /* file with that ext may not exist, ignore */
//             }
//         }
//     }
//
//     /// <summary>Build the public URL for a given avatar_url stored in the profile.</summary>
//     public static string? BuildPublicUrl(string? avatarUrl)
//     {
//         if (string.IsNullOrWhiteSpace(avatarUrl)) return null;
//         // If already a full URL (from storage or external), return as-is
//         if (avatarUrl.StartsWith("http")) return avatarUrl;
//         return $"{PublicBaseUrl}/{avatarUrl}";
//     }
// }