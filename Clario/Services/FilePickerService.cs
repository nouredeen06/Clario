using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace Clario.Services;

public class FilePickerService
{
    public static FilePickerService Instance { get; } = new();
    private static TopLevel? GetTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return TopLevel.GetTopLevel(desktop.MainWindow);
        if (Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime single)
            return TopLevel.GetTopLevel(single.MainView as Visual);
        return null;
    }

    public async Task<IStorageFile?> PickImageAsync()
    {
        var topLevel = GetTopLevel();
        if (topLevel is null) return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Avatar Image",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("Images") { Patterns = ["*.jpg", "*.jpeg", "*.png", "*.webp"] }
            }
        });

        return files.Count > 0 ? files[0] : null;
    }
}
