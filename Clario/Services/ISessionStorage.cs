namespace Clario.Services;

public interface ISessionStorage
{
    void Save(string json);
    string? Load();
    void Delete();
}