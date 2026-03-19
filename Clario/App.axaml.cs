using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using System.Threading;
using Avalonia.Markup.Xaml;
using Clario.Data;
using Clario.Services;
using Clario.ViewModels;
using Clario.Views;

namespace Clario;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();

        var culture = new CultureInfo("en-US");

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        await SupabaseService.InitializeAsync(new FileSessionStorage());
        try
        {
            await SupabaseService.Client.Auth.RetrieveSessionAsync();
        }
        catch
        {
            /* session invalid or expired */
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

            desktop.MainWindow = new MainWindow
            {
                DataContext = user is not null ? new MainViewModel() : new AuthViewModel()
            };
            desktop.MainWindow.Show();
        }

        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = user is not null ? new MainViewModel() : new AuthViewModel()
            };
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