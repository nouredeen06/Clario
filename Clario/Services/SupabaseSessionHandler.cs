using System.Text.Json;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

namespace Clario.Services;

public class SupabaseSessionHandler : IGotrueSessionPersistence<Session>
{
    private readonly ISessionStorage _storage;

    public SupabaseSessionHandler(ISessionStorage storage) => _storage = storage;

    public void SaveSession(Session session)
    {
        _storage.Save(JsonSerializer.Serialize(session));
    }

    public Session? LoadSession()
    {
        var json = _storage.Load();
        return json is null ? null : JsonSerializer.Deserialize<Session>(json);
    }

    public void DestroySession() => _storage.Delete();
}