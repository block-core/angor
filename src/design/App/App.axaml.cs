using System;
using AsyncImageLoader;
using AsyncImageLoader.Loaders;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using App.Automation;
using App.Composition;
using Microsoft.Extensions.DependencyInjection;
using App.UI.Shared;
using App.UI.Shell;
using Optris.Icons.Avalonia;
using Optris.Icons.Avalonia.FontAwesome;

namespace App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static Action<ServiceCollection>? PlatformServices { get; set; }

    public override void Initialize()
    {
        IconProvider.Current.Register<FontAwesomeIconProvider>();

        // Repair stale icon bakes (Optris icons can bake a black snapshot before
        // their Foreground resource resolves — see IconBakeFix docs).
        UI.Shared.Helpers.IconBakeFix.Install();

        // Use disk-cached image loader so AdvancedImage (Header control) doesn't re-download
        // on every attach. Main project/investment images use ImageCacheService instead.
        ImageLoader.AsyncImageLoader = new DiskCachedWebImageLoader(
            System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                "App", "ImageCache"));

        AvaloniaXamlLoader.Load(this);

        // NOTE: mobile BoxShadow neutralisation removed — the original scroll-lag
        // culprit was unbounded remote image decodes (fixed in 932f01ce) and the
        // Avalonia 12 renderer handles shadows fine on Android. Full shadow
        // palette is now shared across desktop and mobile.
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var lifetime = ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var profileName = ProfileNameResolver.GetProfileName(lifetime?.Args);

        // Build DI container with profile-specific data isolation
        Services = CompositionRoot.BuildServiceProvider(profileName, platformServices: PlatformServices);

#if DEBUG
        // Start test automation server if ANGOR_TEST_API=1
        AutomationServer.StartIfEnabled(Services);
#endif

        if (lifetime != null)
        {
            lifetime.MainWindow = new MainWindow();
        }
        else if (ApplicationLifetime is IActivityApplicationLifetime activity)
        {
            // Android (Avalonia 12) — uses the new IActivityApplicationLifetime
            // with a MainViewFactory delegate. Must be checked before
            // ISingleViewApplicationLifetime because IActivityApplicationLifetime
            // also implements ISingleViewApplicationLifetime in some configurations.
            LayoutModeService.Instance.UpdateWidth(400);
            activity.MainViewFactory = () => new ShellView();
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            // iOS / WASM — no window, just set the main view directly.
            // Force mobile layout since there's no resizable window.
            LayoutModeService.Instance.UpdateWidth(400);
            singleView.MainView = new ShellView();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
