using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Clario.Models;
using Clario.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Supabase.Gotrue;

namespace Clario.ViewModels;

public partial class AuthViewModel : ViewModelBase
{
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(ConfirmCreateAccountCommand))]
    private string _firstName;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(ConfirmCreateAccountCommand))]
    private string _lastName;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(ConfirmCreateAccountCommand), nameof(ConfirmLoginCommand))]
    private string _email;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(ConfirmCreateAccountCommand), nameof(ConfirmLoginCommand))]
    private string _password;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(ConfirmCreateAccountCommand))]
    private string _confirmPassword;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(isSignin), nameof(isCreateAccount))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCreateAccountCommand), nameof(ConfirmLoginCommand))]
    private string _operation = "login";

    public AuthViewModel()
    {
        DebugLogger.Log("auth vm loaded");
        setDefaults();
    }

    [Conditional("DEBUG")]
    private void setDefaults()
    {
        if (!File.Exists("devsettings.json")) return;

        var json = File.ReadAllText("devsettings.json");
        var config = JsonSerializer.Deserialize<Wrapper>(json);
        if (config?.TestDefaults is null) return;

        FirstName = config.TestDefaults.FirstName;
        LastName = config.TestDefaults.LastName;
        Email = config.TestDefaults.Email;
        Password = config.TestDefaults.Password;
        ConfirmPassword = config.TestDefaults.Password;

        ThemeService.SwitchToTheme("system");
    }

    [RelayCommand]
    private void SetOperation(string operation)
    {
        Operation = operation;
    }

    [RelayCommand(CanExecute = nameof(canSignin))]
    private async Task ConfirmLogin()
    {
        try
        {
            await SupabaseService.Client.Auth.SignIn(_email, _password);

            var user = SupabaseService.Client.Auth.CurrentUser;

            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow!.DataContext = user is not null ? new MainViewModel() : new AuthViewModel();
            }
            else if (Application.Current.ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
            {
                singleViewPlatform.MainView!.DataContext = user is not null ? new MainViewModel() : new AuthViewModel();
            }
        }
        catch (Exception e)
        {
            DebugLogger.Log(e);
        }
    }

    [RelayCommand(CanExecute = nameof(canCreateAccount))]
    private async Task ConfirmCreateAccount()
    {
        try
        {
            var session = await SupabaseService.Client.Auth.SignUp(
                _email,
                _password,
                new SignUpOptions
                {
                    Data = new Dictionary<string, object>
                    {
                        {
                            "display_name", $"{FirstName.Trim()} {LastName.Trim()}"
                        }
                    }
                });
            if (session is null) return;
            await SupabaseService.Client.Auth.SetSession(session.AccessToken, session.RefreshToken);
            var user = session.User;

            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow!.DataContext = user is not null ? new MainViewModel() : new AuthViewModel();
            }
            else if (Application.Current.ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
            {
                singleViewPlatform.MainView!.DataContext = user is not null ? new MainViewModel() : new AuthViewModel();
            }
        }
        catch (Exception e)
        {
            DebugLogger.Log(e);
        }
    }


    public bool isSignin => Operation == "login";
    public bool isCreateAccount => Operation == "signup";

    public bool canSignin => isSignin && !string.IsNullOrWhiteSpace(_email) && !string.IsNullOrWhiteSpace(_password);

    public bool canCreateAccount => isCreateAccount && !string.IsNullOrWhiteSpace(_firstName) && !string.IsNullOrWhiteSpace(_lastName) &&
                                    !string.IsNullOrWhiteSpace(_email) &&
                                    !string.IsNullOrWhiteSpace(_password) && _password == _confirmPassword;
}

class Wrapper
{
    public TestDefaults TestDefaults { get; set; }
}