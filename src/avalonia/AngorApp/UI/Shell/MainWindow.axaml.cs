using Avalonia.Platform;
using Avalonia.Styling;
using AVVI94.Breakpoints.Avalonia.Collections;
using BP = AVVI94.Breakpoints.Avalonia.Controls.Breakpoints;

namespace AngorApp.UI.Shell;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SetWindowIconForCurrentTheme();

        Application.Current!
            .GetObservable(ThemeVariantScope.ActualThemeVariantProperty)
            .Subscribe(_ => SetWindowIconForCurrentTheme());

        BreakpointList breakpoints = [("XS", 1), ("S", 768)];
        BP.SetValues(this, breakpoints);

        this.GetObservable(BP.CurrentBreakpointProperty)
            .Subscribe(bp => Classes.Set("Compact", bp != "S"));
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
