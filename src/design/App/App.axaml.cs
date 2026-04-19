using System;
using AsyncImageLoader;
using AsyncImageLoader.Loaders;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using App.Composition;
using App.UI.Shell;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.FontAwesome;

namespace App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        IconProvider.Current.Register<FontAwesomeIconProvider>();

        // Use disk-cached image loader so AdvancedImage (Header control) doesn't re-download
        // on every attach. Main project/investment images use ImageCacheService instead.
        ImageLoader.AsyncImageLoader = new DiskCachedWebImageLoader(
            System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                "App", "ImageCache"));

        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var lifetime = ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var profileName = ProfileNameResolver.GetProfileName(lifetime?.Args);

        // Build DI container with profile-specific data isolation
        Services = CompositionRoot.BuildServiceProvider(profileName);

        if (lifetime != null)
        {
            lifetime.MainWindow = new MainWindow();
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = new ShellView();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
