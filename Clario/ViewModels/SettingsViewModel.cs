using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Clario.Data;
using Clario.Models;
using Clario.Models.GeneralModels;
using Clario.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace Clario.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    public required ViewModelBase parentViewModel;
    public GeneralDataRepo AppData => DataRepo.General;

    public static readonly HttpClient _HttpClient = new();


    // ── Profile fields ───────────────────────────────────────
    [ObservableProperty] private string _displayName = "";
    [ObservableProperty] private string _avatarUrl = "";
    [ObservableProperty] private Bitmap? _avatarImage;
    [ObservableProperty] private string _selectedTheme = "system";
    [ObservableProperty] private string _selectedLanguage = "en";

    // ── Account (auth) fields ────────────────────────────────
    [ObservableProperty] private string _maskedEmail = "";
    private string _fullEmail = "";

    // ── Change email flow ────────────────────────────────────
    [ObservableProperty] private bool _isChangingEmail = false;
    [ObservableProperty] private string _newEmail = "";
    [ObservableProperty] private string _emailConfirmPassword = "";

    // ── Change password flow ─────────────────────────────────
    [ObservableProperty] private bool _isChangingPassword = false;
    [ObservableProperty] private string _currentPassword = "";
    [ObservableProperty] private string _newPassword = "";
    [ObservableProperty] private string _confirmNewPassword = "";

    // ── UI state ─────────────────────────────────────────────
    [ObservableProperty] private bool _isSaving = false;
    [ObservableProperty] private bool _isUploadingAvatar = false;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasSuccess))]
    private string? _successMessage;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasEmailSuccess))]
    private string? _emailSuccessMessage;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasEmailError))]
    private string? _emailErrorMessage;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasPasswordSuccess))]
    private string? _passwordSuccessMessage;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasPasswordError))]
    private string? _passwordErrorMessage;

    public bool HasSuccess => !string.IsNullOrEmpty(SuccessMessage);
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasEmailSuccess => !string.IsNullOrEmpty(EmailSuccessMessage);
    public bool HasEmailError => !string.IsNullOrEmpty(EmailErrorMessage);
    public bool HasPasswordSuccess => !string.IsNullOrEmpty(PasswordSuccessMessage);
    public bool HasPasswordError => !string.IsNullOrEmpty(PasswordErrorMessage);
    public bool HasAvatar => !string.IsNullOrEmpty(AvatarUrl);

    // ── Options ──────────────────────────────────────────────

    public ObservableCollection<(string Value, string Label)> Themes { get; } = new()
    {
        ("system", "System default"),
        ("dark", "Dark"),
        ("light", "Light"),
        ("latte", "Catppuccin Latte"),
        ("macchiato", "Catppuccin Macchiato"),
        ("mocha", "Catppuccin Mocha")
    };

    public ObservableCollection<string> ThemeLabels { get; } = new()
    {
        "System default", "Dark", "Light", "Catppuccin Latte", "Catppuccin Macchiato", "Catppuccin Mocha"
    };

    public ObservableCollection<(string Value, string Label)> Languages { get; } = new()
    {
        ("en", "English"),
        ("ar", "العربية"),
    };

    public ObservableCollection<string> LanguageLabels { get; } = new()
    {
        "English", "العربية"
    };

    // ComboBox selected indices (mapped to/from string values)
    [ObservableProperty] private int _selectedThemeIndex = 0;
    [ObservableProperty] private int _selectedLanguageIndex = 0;

    partial void OnSelectedThemeIndexChanged(int value)
    {
        SelectedTheme = value switch { 0 => "system", 1 => "dark", 2 => "light", 3 => "latte", 4 => "macchiato", 5 => "mocha", _ => "system" };
    }

    partial void OnSelectedLanguageIndexChanged(int value)
    {
        SelectedLanguage = value switch { 0 => "en", 1 => "ar", _ => "en" };
    }

    // ── Init ─────────────────────────────────────────────────
    public SettingsViewModel()
    {
        _ = Initialize();
        WeakReferenceMessenger.Default.Register<ProfileUpdated>(this, async (_, m) => { await Initialize(); });
    }

    public async Task Initialize()
    {
        DisplayName = AppData.Profile?.DisplayName ?? "";
        AvatarUrl = DataRepo.General.BuildPublicUrl(AppData.Profile?.AvatarUrl) ?? "";
        AvatarImage = AppData.Profile?.Avatar;
        SelectedTheme = AppData.Profile?.Theme ?? "system";
        SelectedLanguage = AppData.Profile?.Language ?? "en";

        // sync indices
        SelectedThemeIndex = SelectedTheme switch { "dark" => 1, "light" => 2, "latte" => 3, "macchiato" => 4, "mocha" => 5, _ => 0 };
        SelectedLanguageIndex = SelectedLanguage switch { "ar" => 1, _ => 0 };

        // mask email
        _fullEmail = SupabaseService.Client.Auth.CurrentUser?.Email ?? "";
        MaskedEmail = MaskEmail(_fullEmail);
    }


    private static string MaskEmail(string email)
    {
        if (string.IsNullOrEmpty(email)) return "";
        var atIndex = email.IndexOf('@');
        if (atIndex <= 2) return email; // too short to mask
        var local = email[..atIndex];
        var domain = email[atIndex..];
        var visible = local[..2];
        var masked = new string('•', Math.Min(local.Length - 2, 5));
        return $"{visible}{masked}{domain}";
    }

    // ── Avatar commands ───────────────────────────────────────

    [RelayCommand]
    private async Task UploadAvatar()
    {
        var file = await FilePickerService.Instance.PickImageAsync();
        if (file is null) return;

        IsUploadingAvatar = true;
        ErrorMessage = null;
        SuccessMessage = null;

        try
        {
            var localPath = file.Path.LocalPath;
            var url = await DataRepo.General.UploadAvatarAsync(localPath);
            AvatarUrl = url;

            // persist to profile
            await DataRepo.General.UpdateProfileAvatar(url);
            SuccessMessage = "Avatar updated successfully.";
            await Initialize();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to upload avatar. Please try again.";
            DebugLogger.Log(ex);
        }
        finally
        {
            IsUploadingAvatar = false;
        }
    }

    [RelayCommand]
    private async Task RemoveAvatar()
    {
        IsUploadingAvatar = true;
        ErrorMessage = null;
        SuccessMessage = null;

        try
        {
            await DataRepo.General.DeleteAvatarAsync();
            await DataRepo.General.UpdateProfileAvatar(null);
            AvatarUrl = "";
            SuccessMessage = "Avatar removed.";
            await Initialize();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to remove avatar.";
            DebugLogger.Log(ex);
        }
        finally
        {
            IsUploadingAvatar = false;
        }
    }

    // ── Save profile ─────────────────────────────────────────

    [RelayCommand]
    private async Task SaveProfile()
    {
        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            ErrorMessage = "Display name cannot be empty.";
            return;
        }

        IsSaving = true;
        ErrorMessage = null;
        SuccessMessage = null;

        try
        {
            var updated = new Profile
            {
                Id = AppData.Profile.Id,
                DisplayName = DisplayName.Trim(),
                Currency = AppData.Profile?.Currency ?? "USD",
                Theme = SelectedTheme,
                Language = SelectedLanguage,
                AvatarUrl = AppData.Profile.AvatarUrl,
                Avatar = AppData.Profile.Avatar,
                SavingsGoal = AppData.Profile.SavingsGoal
            };

            await DataRepo.General.UpdateProfile(updated);

            // apply theme immediately
            ThemeService.SwitchToTheme(SelectedTheme);

            SuccessMessage = "Profile saved successfully.";
            await Initialize();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to save profile. Please try again.";
            DebugLogger.Log(ex);
        }
        finally
        {
            IsSaving = false;
        }
    }

    // ── Change email ─────────────────────────────────────────

    [RelayCommand]
    private void StartChangeEmail()
    {
        NewEmail = "";
        EmailConfirmPassword = "";
        EmailErrorMessage = null;
        EmailSuccessMessage = null;
        IsChangingEmail = true;
    }

    [RelayCommand]
    private void CancelChangeEmail()
    {
        IsChangingEmail = false;
        EmailErrorMessage = null;
        EmailSuccessMessage = null;
    }

    [RelayCommand]
    private async Task ConfirmChangeEmail()
    {
        EmailErrorMessage = null;
        EmailSuccessMessage = null;

        if (string.IsNullOrWhiteSpace(NewEmail) || !NewEmail.Contains('@'))
        {
            EmailErrorMessage = "Please enter a valid email address.";
            return;
        }

        if (string.IsNullOrWhiteSpace(EmailConfirmPassword))
        {
            EmailErrorMessage = "Please enter your current password to confirm.";
            return;
        }

        IsSaving = true;
        try
        {
            // re-authenticate first to confirm password
            await SupabaseService.Client.Auth.SignIn(_fullEmail, EmailConfirmPassword);

            // update email — Supabase sends confirmation to the new address
            await SupabaseService.Client.Auth.Update(new Supabase.Gotrue.UserAttributes
            {
                Email = NewEmail.Trim()
            });

            EmailSuccessMessage = "Confirmation sent to your new email address. Please check your inbox.";
            IsChangingEmail = false;
        }
        catch (Exception ex)
        {
            EmailErrorMessage = "Failed to update email. Check your password and try again.";
            DebugLogger.Log(ex);
        }
        finally
        {
            IsSaving = false;
        }
    }

    // ── Change password ──────────────────────────────────────

    [RelayCommand]
    private void StartChangePassword()
    {
        CurrentPassword = "";
        NewPassword = "";
        ConfirmNewPassword = "";
        PasswordErrorMessage = null;
        PasswordSuccessMessage = null;
        IsChangingPassword = true;
    }

    [RelayCommand]
    private void CancelChangePassword()
    {
        IsChangingPassword = false;
        PasswordErrorMessage = null;
        PasswordSuccessMessage = null;
    }

    [RelayCommand]
    private async Task ConfirmChangePassword()
    {
        PasswordErrorMessage = null;
        PasswordSuccessMessage = null;

        if (string.IsNullOrWhiteSpace(CurrentPassword))
        {
            PasswordErrorMessage = "Please enter your current password.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 8)
        {
            PasswordErrorMessage = "New password must be at least 8 characters.";
            return;
        }

        if (NewPassword != ConfirmNewPassword)
        {
            PasswordErrorMessage = "Passwords do not match.";
            return;
        }

        IsSaving = true;
        try
        {
            // re-authenticate to confirm current password
            await SupabaseService.Client.Auth.SignIn(_fullEmail, CurrentPassword);

            await SupabaseService.Client.Auth.Update(new Supabase.Gotrue.UserAttributes
            {
                Password = NewPassword
            });

            PasswordSuccessMessage = "Password updated successfully.";
            IsChangingPassword = false;
        }
        catch (Exception ex)
        {
            PasswordErrorMessage = "Failed to update password. Check your current password and try again.";
            DebugLogger.Log(ex);
        }
        finally
        {
            IsSaving = false;
        }
    }

    // ── Sign out ─────────────────────────────────────────────

    [RelayCommand]
    private async Task SignOut()
    {
        try
        {
            await ((MainViewModel)parentViewModel).SignOutCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Failed to sign out.";
            DebugLogger.Log(ex);
        }
    }
}