using System;
using System.Linq;
using Avalonia;
using Clario;

namespace Clario.Desktop;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Capture deep link passed as command-line arg by Windows protocol handler
        var deepLink = args.FirstOrDefault(a =>
            a.StartsWith("clario://", StringComparison.OrdinalIgnoreCase));
        if (deepLink != null)
            App.PendingDeepLink = deepLink;

        // Register clario:// URL scheme on Windows (idempotent)
        RegisterUrlScheme();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static void RegisterUrlScheme()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            var exe = Environment.ProcessPath
                ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (exe is null) return;

            using var key = Microsoft.Win32.Registry.CurrentUser
                .CreateSubKey(@"SOFTWARE\Classes\clario");
            key.SetValue("", "URL:Clario Protocol");
            key.SetValue("URL Protocol", "");
            using var cmd = key.CreateSubKey(@"shell\open\command");
            cmd.SetValue("", $"\"{exe}\" \"%1\"");
        }
        catch { /* ignore — no registry write access in sandboxed environments */ }
    }
}
