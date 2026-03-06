using System;
using AsyncImageLoader;
using AsyncImageLoader.Loaders;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia2.Composition;
using Avalonia2.UI.Shell;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.FontAwesome;

namespace Avalonia2;

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
                "Avalonia2", "ImageCache"));

        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var lifetime = ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var profileName = GetProfileName(lifetime?.Args);

        // Build DI container with profile-specific data isolation
        Services = CompositionRoot.BuildServiceProvider(profileName);

        if (lifetime != null)
        {
            lifetime.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private const string DefaultProfileName = "Default";
    private const string ProfileOption = "--profile";

    private static string GetProfileName(string[]? args)
    {
        if (args == null || args.Length == 0)
            return DefaultProfileName;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (string.IsNullOrWhiteSpace(argument))
                continue;

            if (argument.StartsWith(ProfileOption + "=", StringComparison.OrdinalIgnoreCase))
            {
                var value = argument.Substring(ProfileOption.Length + 1).Trim();
                return string.IsNullOrWhiteSpace(value) ? DefaultProfileName : value;
            }

            if (!string.Equals(argument, ProfileOption, StringComparison.OrdinalIgnoreCase))
                continue;

            if (index + 1 < args.Length)
            {
                var value = args[index + 1]?.Trim();
                if (!string.IsNullOrWhiteSpace(value) && !value.StartsWith("--", StringComparison.Ordinal))
                    return value;
            }

            return DefaultProfileName;
        }

        return DefaultProfileName;
    }
}
