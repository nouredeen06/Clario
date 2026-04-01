using System;
using System.Globalization;
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
            Console.WriteLine("ANDROID PATH HIT");
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
            Console.WriteLine($"[Auth] RetrieveSession failed: {e.Message}");
        }

        var user = SupabaseService.Client.Auth.CurrentUser;

        var profile = await DataRepo.General.FetchProfileInfo();
        if (profile is not null)
        {
            ThemeService.SwitchToTheme(profile.Theme);
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            desktop.MainWindow!.DataContext = user is not null ? new MainViewModel() : new AuthViewModel();
        }

        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            Console.WriteLine("ANDROID PATH HIT");
            singleViewPlatform.MainView!.DataContext = user is not null ? new MainViewModel() : new AuthViewModel();
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