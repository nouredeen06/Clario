using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace Clario.ViewModels;

public enum MessageType { Error, Warning, Success, Info }

public partial class MessageBoxViewModel : ViewModelBase
{
    [ObservableProperty] private MessageType _type = MessageType.Info;
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _message = "";

    public bool IsError   => Type == MessageType.Error;
    public bool IsWarning => Type == MessageType.Warning;
    public bool IsSuccess => Type == MessageType.Success;
    public bool IsInfo    => Type == MessageType.Info;

    public Action? OnClose { get; set; }

    partial void OnTypeChanged(MessageType value)
    {
        OnPropertyChanged(nameof(IsError));
        OnPropertyChanged(nameof(IsWarning));
        OnPropertyChanged(nameof(IsSuccess));
        OnPropertyChanged(nameof(IsInfo));
    }

    [RelayCommand]
    private void Close() => OnClose?.Invoke();
}
