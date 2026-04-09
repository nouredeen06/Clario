using System;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Clario.Data;
using Clario.Services;
using Clario.ViewModels;
using Clario.Views;

namespace Clario;

public partial class App : Application
{
    public static bool IsMobile { get; private set; }

    /// <summary>Set before OnFrameworkInitializationCompleted runs (from Program.cs or MainActivity).</summary>
    public static string? PendingDeepLink { get; set; }

    /// <summary>Called from MainActivity.OnNewIntent when app is already running.</summary>
    public static async Task HandleDeepLink(string deepLink)
    {
        var (accessToken, refreshToken, type) = ParseDeepLinkFragment(deepLink);
        if (type != "recovery" || accessToken is null) return;

        try { await SupabaseService.Client.Auth.SetSession(accessToken, refreshToken); } catch { }

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            var vm = new ResetPasswordViewModel();
            if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.MainWindow!.DataContext = vm;
            else if (Current?.ApplicationLifetime is ISingleViewApplicationLifetime sv)
                sv.MainView!.DataContext = vm;
        });
    }

    private static (string? accessToken, string? refreshToken, string? type) ParseDeepLinkFragment(string url)
    {
        var hash = url.IndexOf('#');
        if (hash < 0) return default;
        string? at = null, rt = null, type = null;
        foreach (var part in url[(hash + 1)..].Split('&'))
        {
            var eq = part.IndexOf('=');
            if (eq < 0) continue;
            var val = Uri.UnescapeDataString(part[(eq + 1)..]);
            switch (part[..eq])
            {
                case "access_token":  at   = val; break;
                case "refresh_token": rt   = val; break;
                case "type":          type = val; break;
            }
        }
        return (at, rt, type);
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        RequestedThemeVariant = ThemeVariant.Dark;
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLoading)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            desktopLoading.MainWindow = new MainWindow
            {
                DataContext = new LoadingViewModel()
            };
            desktopLoading.MainWindow.Show();
        }

        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatformLoading)
        {
            DebugLogger.Log("ANDROID PATH HIT");
            singleViewPlatformLoading.MainView = new MainAppMobile()
            {
                DataContext = new LoadingViewModel()
            };
        }

        IsMobile = ApplicationLifetime is ISingleViewApplicationLifetime;

        var culture = new CultureInfo("en-US");

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        await SupabaseService.InitializeAsync(new FileSessionStorage());
        try
        {
            await SupabaseService.Client.Auth.RetrieveSessionAsync();
        }
        catch (Exception e)
        {
            DebugLogger.Log($"[Auth] RetrieveSession failed: {e.Message}");
        }

        var user = SupabaseService.Client.Auth.CurrentUser;

        var profile = await DataRepo.General.FetchProfileInfo();
        if (profile is not null)
        {
            ThemeService.SwitchToTheme(profile.Theme);
        }

        // Check for deep link from password reset email
        ViewModelBase targetViewModel;
        if (PendingDeepLink is { } deepLink && deepLink.Contains("type=recovery"))
        {
            var (accessToken, refreshToken, _) = ParseDeepLinkFragment(deepLink);
            if (accessToken is not null)
            {
                try { await SupabaseService.Client.Auth.SetSession(accessToken, refreshToken); } catch { }
            }
            PendingDeepLink = null;
            targetViewModel = new ResetPasswordViewModel();
        }
        else
        {
            targetViewModel = user is not null ? new MainViewModel() : new AuthViewModel();
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow!.DataContext = targetViewModel;
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            DebugLogger.Log("ANDROID PATH HIT");
            singleViewPlatform.MainView!.DataContext = targetViewModel;
        }
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}