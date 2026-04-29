using System;
using AsyncImageLoader;
using AsyncImageLoader.Loaders;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using App.Composition;
using Microsoft.Extensions.DependencyInjection;
using App.UI.Shared;
using App.UI.Shell;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.FontAwesome;

namespace App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static Action<ServiceCollection>? PlatformServices { get; set; }

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

        // ── Mobile perf: neutralise BoxShadow resources on touch platforms ──
        // Box-shadow rendering is one of the most expensive ops on Skia/Android.
        // Overriding the three shared shadow resources to "no shadow" here cascades
        // to every card across the app (ProjectCard, Portfolio, Settings, Funds,
        // CreateProject, InvestmentDetail, ProjectDetail, etc.) without touching
        // individual views. Desktop retains the full shadow palette unchanged.
        //
        // Cards keep their 1px border and rounded corners for visual identity;
        // only the drop-shadow is removed on mobile.
        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
        {
            var none = new Avalonia.Media.BoxShadows(default);
            Resources["ItemShadow"] = none;
            Resources["ItemShadowHover"] = none;
            Resources["ItemShadowBig"] = none;
            Resources["FloatingBtnShadow"] = none;
            // Also neutralise button shadows used by Home's call-to-action
            // buttons (wrapped in Border.ButtonShadow). These are per-button
            // drop shadows that compound cost on Home's two hero cards.
            Resources["PrimaryButtonShadow"] = none;
            Resources["PrimaryButtonShadowHover"] = none;
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var lifetime = ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var profileName = ProfileNameResolver.GetProfileName(lifetime?.Args);

        // Build DI container with profile-specific data isolation
        Services = CompositionRoot.BuildServiceProvider(profileName, platformServices: PlatformServices);

        if (lifetime != null)
        {
            lifetime.MainWindow = new MainWindow();
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            // Android / iOS / WASM — no window, just set the main view directly.
            // Force mobile layout since there's no resizable window.
            LayoutModeService.Instance.UpdateWidth(400);
            singleView.MainView = new ShellView();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
