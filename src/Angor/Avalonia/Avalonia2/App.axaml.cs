using AsyncImageLoader;
using AsyncImageLoader.Loaders;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia2.UI.Shell;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.FontAwesome;

namespace Avalonia2;

public partial class App : Application
{
    public override void Initialize()
    {
        IconProvider.Current.Register<FontAwesomeIconProvider>();

        // Use disk-cached image loader so AdvancedImage (Header control) doesn't re-download
        // on every attach. Main project/investment images use ImageCacheService instead.
        ImageLoader.AsyncImageLoader = new DiskCachedWebImageLoader(
            System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                "Avalonia2", "ImageCache"));

        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = new ShellView();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
