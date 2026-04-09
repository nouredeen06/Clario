using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Clario.Enums;
using Clario.Models;
using Clario.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;

namespace Clario.ViewModels;

public partial class AuthViewModel : ViewModelBase
{
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(ConfirmCreateAccountCommand))]
    private string _firstName;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(ConfirmCreateAccountCommand))]
    private string _lastName;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(ConfirmCreateAccountCommand), nameof(ConfirmLoginCommand), nameof(SendResetLinkCommand))]
    private string _email;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(ConfirmCreateAccountCommand), nameof(ConfirmLoginCommand))]
    private string _password;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(ConfirmCreateAccountCommand))]
    private string _confirmPassword;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(isSignin), nameof(isCreateAccount), nameof(isForgotPassword), nameof(ShowTabs))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCreateAccountCommand), nameof(ConfirmLoginCommand), nameof(SendResetLinkCommand))]
    private string _operation = "login";

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    [ObservableProperty] private bool _resetEmailSent;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

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
        ErrorMessage = null;
        ResetEmailSent = false;
    }

    [RelayCommand(CanExecute = nameof(canSendResetLink))]
    private async Task SendResetLink()
    {
        ErrorMessage = null;
        try
        {
            await SupabaseService.Client.Auth.ResetPasswordForEmail(_email);
            ResetEmailSent = true;
        }
        catch (GotrueException e)
        {
            DebugLogger.Log(e);
            ErrorMessage = e.Reason == FailureHint.Reason.UserBadEmailAddress
                ? GetErrorMessage(AuthError.InvalidEmail)
                : GetErrorMessage(AuthError.Unknown);
        }
        catch (Exception e)
        {
            DebugLogger.Log(e);
            ErrorMessage = GetErrorMessage(AuthError.Unknown);
        }
    }

    [RelayCommand(CanExecute = nameof(canSignin))]
    private async Task ConfirmLogin()
    {
        ErrorMessage = null;
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
        catch (GotrueException e)
        {
            DebugLogger.Log(e);
            ErrorMessage = GetLoginErrorMessage(e.Reason);
        }
        catch (Exception e)
        {
            DebugLogger.Log(e);
            ErrorMessage = GetErrorMessage(AuthError.Unknown);
        }
    }

    [RelayCommand(CanExecute = nameof(canCreateAccount))]
    private async Task ConfirmCreateAccount()
    {
        ErrorMessage = null;
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
        catch (GotrueException e)
        {
            DebugLogger.Log(e);
            ErrorMessage = GetSignupErrorMessage(e.Reason);
        }
        catch (Exception e)
        {
            DebugLogger.Log(e);
            ErrorMessage = GetErrorMessage(AuthError.Unknown);
        }
    }

    private static string GetLoginErrorMessage(FailureHint.Reason reason) => reason switch
    {
        FailureHint.Reason.UserBadLogin     => GetErrorMessage(AuthError.InvalidCredentials),
        FailureHint.Reason.UserBadPassword  => GetErrorMessage(AuthError.InvalidCredentials),
        FailureHint.Reason.UserEmailNotConfirmed => GetErrorMessage(AuthError.EmailNotConfirmed),
        FailureHint.Reason.UserTooManyRequests   => GetErrorMessage(AuthError.RateLimited),
        FailureHint.Reason.UserBadEmailAddress   => GetErrorMessage(AuthError.InvalidEmail),
        FailureHint.Reason.Offline               => GetErrorMessage(AuthError.Unknown),
        _                                        => GetErrorMessage(AuthError.Unknown),
    };

    private static string GetSignupErrorMessage(FailureHint.Reason reason) => reason switch
    {
        FailureHint.Reason.UserAlreadyRegistered => GetErrorMessage(AuthError.EmailAlreadyExists),
        FailureHint.Reason.UserBadPassword       => GetErrorMessage(AuthError.WeakPassword),
        FailureHint.Reason.UserBadEmailAddress   => GetErrorMessage(AuthError.InvalidEmail),
        FailureHint.Reason.UserTooManyRequests   => GetErrorMessage(AuthError.RateLimited),
        FailureHint.Reason.Offline               => GetErrorMessage(AuthError.Unknown),
        _                                        => GetErrorMessage(AuthError.Unknown),
    };

    private static string GetErrorMessage(AuthError error) => error switch
    {
        AuthError.InvalidCredentials => "Invalid email or password.",
        AuthError.EmailAlreadyExists => "An account with this email already exists.",
        AuthError.EmailNotConfirmed  => "Please confirm your email before signing in.",
        AuthError.WeakPassword       => "Password must be at least 6 characters.",
        AuthError.InvalidEmail       => "Please enter a valid email address.",
        AuthError.SignupDisabled     => "Sign-ups are currently disabled.",
        AuthError.RateLimited        => "Too many attempts. Please wait and try again.",
        AuthError.SessionExpired     => "Your session has expired. Please sign in again.",
        _                            => "Something went wrong. Please try again.",
    };


    public bool isSignin => Operation == "login";
    public bool isCreateAccount => Operation == "signup";
    public bool isForgotPassword => Operation == "forgotPassword";
    public bool ShowTabs => !isForgotPassword;

    public bool canSignin => isSignin && !string.IsNullOrWhiteSpace(_email) && !string.IsNullOrWhiteSpace(_password);

    public bool canCreateAccount => isCreateAccount && !string.IsNullOrWhiteSpace(_firstName) && !string.IsNullOrWhiteSpace(_lastName) &&
                                    !string.IsNullOrWhiteSpace(_email) &&
                                    !string.IsNullOrWhiteSpace(_password) && _password == _confirmPassword;

    public bool canSendResetLink => isForgotPassword && !string.IsNullOrWhiteSpace(_email);
}

class Wrapper
{
    public TestDefaults TestDefaults { get; set; }
}