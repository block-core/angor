using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia2.UI.Shared;

namespace Avalonia2.UI.Shell;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SetWindowIconForCurrentTheme();
        Application.Current!
            .GetObservable(ThemeVariantScope.ActualThemeVariantProperty)
            .Subscribe(_ => SetWindowIconForCurrentTheme());

        // Wire LayoutModeService — track window width for responsive breakpoints
        // Vue: lg = 1024px (sidebar visible), md = 768px (tablet)
        this.GetObservable(ClientSizeProperty).Subscribe(size =>
        {
            LayoutModeService.Instance.UpdateWidth(size.Width);
        });
    }

    private void SetWindowIconForCurrentTheme()
    {
        var isLight = Application.Current?.ActualThemeVariant == ThemeVariant.Light;
        var asset = isLight
            ? "avares://Avalonia2/Assets/angor-app-icon-light.png"
            : "avares://Avalonia2/Assets/angor-app-icon-dark.png";

        try
        {
            using var stream = AssetLoader.Open(new Uri(asset));
            Icon = new WindowIcon(stream);
        }
        catch
        {
            var fallback = "avares://Avalonia2/Assets/angor-logo.ico";
            using var stream = AssetLoader.Open(new Uri(fallback));
            Icon = new WindowIcon(stream);
        }
    }
}
