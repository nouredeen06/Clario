using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Clario.Models;
using Clario.ViewModels;

namespace Clario.MobileViews;

public partial class AccountsViewMobile : UserControl
{
    private bool _sheetVisible;

    private TranslateTransform SheetTranslate =>
        (TranslateTransform)BottomSheet.RenderTransform!;

    public AccountsViewMobile()
    {
        InitializeComponent();

        DimOverlay.PointerPressed += async (_, _) => await HideSheet();
        CloseButton.Click += async (_, _) => await HideSheet();

        AddHandler(Button.ClickEvent, async (_, e) =>
        {
            if (e.Source is Button { DataContext: Account }) await ShowSheet();
        }, handledEventsToo: false);

        DataContextChanged += (_, _) =>
        {
            if (DataContext is AccountsViewModel vm)
            {
                vm.TryCloseSheet = () =>
                {
                    if (!_sheetVisible) return false;
                    _ = HideSheet();
                    return true;
                };

                vm.PropertyChanged += async (_, args) =>
                {
                    if (args.PropertyName == nameof(AccountsViewModel.ShouldCloseSheet) && vm.ShouldCloseSheet)
                    {
                        await HideSheet();
                        vm.ShouldCloseSheet = false;
                    }
                };
            }
        };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        BottomSheet.MaxHeight = Bounds.Height * 0.82;

        // update if screen size changes
        PropertyChanged += (_, args) =>
        {
            if (args.Property == BoundsProperty)
                BottomSheet.MaxHeight = Bounds.Height * 0.82;
        };
    }

    public async Task ShowSheet()
    {
        if (_sheetVisible) return;
        _sheetVisible = true;

        OverlayGrid.IsVisible = true;
        DimOverlay.Opacity = 0;
        SheetTranslate.Y = 800;

        var sheetAnim = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(320),
            Easing = new CubicEaseOut(),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(TranslateTransform.YProperty, 800d) } },
                new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(TranslateTransform.YProperty, 0d) } }
            }
        };

        var dimAnim = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(220),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(OpacityProperty, 0d) } },
                new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(OpacityProperty, 1d) } }
            }
        };

        await Task.WhenAll(sheetAnim.RunAsync(BottomSheet), dimAnim.RunAsync(DimOverlay));

        SheetTranslate.Y = 0;
        DimOverlay.Opacity = 1;
    }

    public async Task HideSheet()
    {
        if (!_sheetVisible) return;
        _sheetVisible = false;

        var sheetAnim = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(260),
            Easing = new CubicEaseIn(),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(TranslateTransform.YProperty, 0d) } },
                new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(TranslateTransform.YProperty, 800d) } }
            }
        };

        var dimAnim = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(200),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(OpacityProperty, 1d) } },
                new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(OpacityProperty, 0d) } }
            }
        };

        await Task.WhenAll(sheetAnim.RunAsync(BottomSheet), dimAnim.RunAsync(DimOverlay));

        OverlayGrid.IsVisible = false;
        SheetTranslate.Y = 0;
        DimOverlay.Opacity = 1;
    }
}
