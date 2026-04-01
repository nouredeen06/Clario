using System;
using System.IO;

namespace Clario.Services;

public class FileSessionStorage : ISessionStorage
{
    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Clario", "session.json");

    public void Save(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, json);
    }

    public string? Load()
    {
        if (!File.Exists(_path)) return null;

        var json = File.ReadAllText(_path);
        return json;
    }
   

    public void Delete()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }
}