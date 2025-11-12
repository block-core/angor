using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Styling;

namespace AngorApp.UI.Sections.Shell;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SetWindowIconForCurrentTheme();
        // React to theme changes to keep the window icon in sync (Windows/Linux)
        Application.Current!
            .GetObservable(ThemeVariantScope.ActualThemeVariantProperty)
            .Subscribe(_ => SetWindowIconForCurrentTheme());
    }

    private void SetWindowIconForCurrentTheme()
    {
        var isLight = Application.Current?.ActualThemeVariant == ThemeVariant.Light;
        var asset = isLight
            ? "avares://AngorApp/Assets/angor-app-icon-light.png"
            : "avares://AngorApp/Assets/angor-app-icon-dark.png";

        try
        {
            using var stream = AssetLoader.Open(new Uri(asset));
            Icon = new WindowIcon(stream);
        }
        catch
        {
            // Fallback to existing ICO if something goes wrong
            var fallback = "avares://AngorApp/Assets/angor-logo.ico";
            using var stream = AssetLoader.Open(new Uri(fallback));
            Icon = new WindowIcon(stream);
        }
    }
}
