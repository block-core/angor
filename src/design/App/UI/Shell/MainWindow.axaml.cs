using Avalonia.Platform;
using Avalonia.Styling;

namespace App.UI.Shell;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SetWindowIconForCurrentTheme();
        Application.Current!
            .GetObservable(ThemeVariantScope.ActualThemeVariantProperty)
            .Subscribe(_ => SetWindowIconForCurrentTheme());
    }

    private void SetWindowIconForCurrentTheme()
    {
        var isLight = Application.Current?.ActualThemeVariant == ThemeVariant.Light;
        var asset = isLight
            ? "avares://App/Assets/angor-app-icon-light.png"
            : "avares://App/Assets/angor-app-icon-dark.png";

        try
        {
            using var stream = AssetLoader.Open(new Uri(asset));
            Icon = new WindowIcon(stream);
        }
        catch
        {
            var fallback = "avares://App/Assets/angor-logo.ico";
            using var stream = AssetLoader.Open(new Uri(fallback));
            Icon = new WindowIcon(stream);
        }
    }
}
