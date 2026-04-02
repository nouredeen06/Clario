using System;
using System.Text.Json;
using System.Threading.Tasks;
using Supabase;

namespace Clario.Services;

public class SupabaseService
{
    private static Client? _client;
    public static Client Client => _client ?? throw new InvalidOperationException("Call InitializeAsync First");

    public static async Task InitializeAsync(ISessionStorage sessionStorage)
    {
        _client = new Client(
            "https://xzxstbllaivumhtpctmo.supabase.co",
            "sb_publishable_cUgUrWvlcGp9Ghnwbfrbnw_ixg_NH7f",
            new SupabaseOptions
            {
                AutoRefreshToken = true,
                AutoConnectRealtime = true,
                SessionHandler = new SupabaseSessionHandler(sessionStorage)
            }
        );

        await _client.InitializeAsync();

        var json = sessionStorage.Load();
        if (json is null) return;
        var session = JsonSerializer.Deserialize<Supabase.Gotrue.Session>(json);
        if (session?.AccessToken is not null && session.RefreshToken is not null)
        {
            try
            {
                await _client.Auth.SetSession(session.AccessToken, session.RefreshToken);
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"Session restore failed: {ex.Message}");
                sessionStorage.Delete(); // session invalid, delete it
            }
        }
    }
}