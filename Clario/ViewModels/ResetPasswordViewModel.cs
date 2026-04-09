using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Clario.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;

namespace Clario.ViewModels;

public partial class ResetPasswordViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SetNewPasswordCommand))]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SetNewPasswordCommand))]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    [ObservableProperty] private bool _passwordUpdated;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    [RelayCommand(CanExecute = nameof(CanSetPassword))]
    private async Task SetNewPassword()
    {
        ErrorMessage = null;
        try
        {
            await SupabaseService.Client.Auth.Update(new UserAttributes { Password = _newPassword });
            PasswordUpdated = true;
        }
        catch (GotrueException e)
        {
            DebugLogger.Log(e);
            ErrorMessage = e.Reason == FailureHint.Reason.UserBadPassword
                ? "Password must be at least 6 characters."
                : "Something went wrong. Please try again.";
        }
        catch (Exception e)
        {
            DebugLogger.Log(e);
            ErrorMessage = "Something went wrong. Please try again.";
        }
    }

    [RelayCommand]
    private void GoToSignIn()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow!.DataContext = new AuthViewModel();
        else if (Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime sv)
            sv.MainView!.DataContext = new AuthViewModel();
    }

    private bool CanSetPassword =>
        !string.IsNullOrWhiteSpace(_newPassword) &&
        _newPassword.Length >= 6 &&
        _newPassword == _confirmPassword;
}
