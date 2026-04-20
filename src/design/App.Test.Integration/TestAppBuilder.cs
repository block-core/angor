using System.Reflection;
using Angor.Shared;
using Angor.Shared.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using App.Composition;
using App.Test.Integration.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.FontAwesome;

[assembly: AvaloniaTestApplication(typeof(App.Test.Integration.TestAppBuilder))]

namespace App.Test.Integration;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());

    public static void RefreshServicesForCurrentProfile()
    {
        ConfigureServices(TestProfileNameProvider.Current);
    }

    private static void ConfigureServices(string profileName)
    {
        var existingServices = global::App.App.Services;
        if (existingServices is IDisposable disposable)
        {
            disposable.Dispose();
        }

        var services = CompositionRoot.BuildServiceProvider(profileName, enableConsoleLogging: true);
        var prop = typeof(global::App.App).GetProperty("Services", BindingFlags.Public | BindingFlags.Static)!;
        prop.SetValue(null, services);

        // Override relays with the test relay for reliable test execution
        var networkStorage = services.GetRequiredService<INetworkStorage>();
        var settings = networkStorage.GetSettings();
        settings.Relays = new List<SettingsUrl>
        {
            new() { Name = "Test Relay", Url = "wss://test.thedude.cloud", IsPrimary = true }
        };
        networkStorage.SetSettings(settings);
    }
}

/// <summary>
/// Full Avalonia Application for headless integration tests.
/// Loads the complete app theme (Theme.axaml with all styles, resources, and Automation.axaml),
/// registers the FontAwesome icon provider, and initializes the real DI container with an
/// isolated "test-send-receive" profile so tests have their own wallet/storage data.
/// </summary>
public class TestApp : Application
{
    /// <summary>
    /// Default test profile used when a test does not override args.
    /// </summary>
    public const string TestProfileName = "test-send-receive";

    private static bool _iconProviderRegistered;

    public override void Initialize()
    {
        // Register FontAwesome so <i:Icon Value="fa-solid fa-..." /> works in headless mode.
        // Guard against double registration since headless tests may create multiple TestApp
        // instances but IconProvider.Current is a process-level singleton.
        if (!_iconProviderRegistered)
        {
            IconProvider.Current.Register<FontAwesomeIconProvider>();
            _iconProviderRegistered = true;
        }

        // Load the full app theme — same Theme.axaml the real app uses.
        // This includes FluentTheme, all custom styles, control templates,
        // resources (colors, icons, tokens), and Automation.axaml.
        Styles.Add(new StyleInclude(new Uri("avares://App"))
        {
            Source = new Uri("avares://App/UI/Themes/V2/Theme.axaml")
        });

        TestAppBuilder.RefreshServicesForCurrentProfile();
    }
}
