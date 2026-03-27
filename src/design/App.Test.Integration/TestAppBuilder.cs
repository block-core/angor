using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using App.Composition;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.FontAwesome;

[assembly: AvaloniaTestApplication(typeof(App.Test.Integration.TestAppBuilder))]

namespace App.Test.Integration;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());
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
    /// The profile name used for integration test isolation.
    /// Data stored under ~/.local/share/App/Profiles/test-send-receive/
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

        // Build the real DI container with test profile isolation
        var services = CompositionRoot.BuildServiceProvider(TestProfileName);

        // App.Services has a private setter — use reflection to set it from the test.
        // This is safe because integration tests intentionally exercise the real app.
        var prop = typeof(global::App.App).GetProperty("Services", BindingFlags.Public | BindingFlags.Static)!;
        prop.SetValue(null, services);
    }
}
