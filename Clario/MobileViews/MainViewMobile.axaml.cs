using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Clario.ViewModels;

namespace Clario.MobileViews;

public partial class MainViewMobile : UserControl
{
    public MainViewMobile()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel != null)
            topLevel.BackRequested += OnBackRequested;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel != null)
            topLevel.BackRequested -= OnBackRequested;
    }

    private void OnBackRequested(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            e.Handled = vm.HandleBackNavigation();
    }
}