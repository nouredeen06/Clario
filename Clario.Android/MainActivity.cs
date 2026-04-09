using System;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Avalonia;
using Avalonia.Android;
using Clario;

namespace Clario.Android;

[Activity(
    Label = "Clario",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "clario",
    DataHost = "auth")]
public class MainActivity : AvaloniaMainActivity<App>
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Capture deep link before Avalonia initializes
        var uri = Intent?.DataString;
        if (uri?.StartsWith("clario://", StringComparison.OrdinalIgnoreCase) == true)
            App.PendingDeepLink = uri;

        base.OnCreate(savedInstanceState);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        // Called when app is already running (SingleTop) and link is opened again
        var uri = intent?.DataString;
        if (uri?.StartsWith("clario://", StringComparison.OrdinalIgnoreCase) == true)
            _ = App.HandleDeepLink(uri);
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}
