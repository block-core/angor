using AngorApp.Composition;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Zafiro.Avalonia.Icons;
using Zafiro.Avalonia.Misc;
using Humanizer.Configuration;
using AngorApp.Localization;
using AngorApp.UI.Shell;

namespace AngorApp;

public partial class App : Application
{
    public override void Initialize()
    {
        // Register Humanizer strategy to prefer "in X" over "X from now" in English
        Configurator.DateTimeHumanizeStrategy = new InPrepositionDateTimeHumanizeStrategy();

        IconControlProviderRegistry.Register(new ProjektankerIconControlProvider(), asDefault: true);
        IconControlProviderRegistry.Register(new SvgIconControlProvider());

        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (Avalonia.Controls.Design.IsDesignMode)
        {
            base.OnFrameworkInitializationCompleted();
            return;
        }
        
        var lifetime = ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var profileName = GetProfileName(lifetime?.Args);

        this.Connect(
            () => new ShellView(),
            view => CompositionRoot.CreateMainViewModel(view, profileName),
            () => new MainWindow());

        base.OnFrameworkInitializationCompleted();
    }

    private const string DefaultProfileName = "Default";
    private const string ProfileOption = "--profile";

    private static string GetProfileName(string[]? args)
    {
        if (args == null || args.Length == 0)
        {
            return DefaultProfileName;
        }

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (string.IsNullOrWhiteSpace(argument))
            {
                continue;
            }

            if (argument.StartsWith(ProfileOption + "=", StringComparison.OrdinalIgnoreCase))
            {
                var value = argument.Substring(ProfileOption.Length + 1).Trim();
                return string.IsNullOrWhiteSpace(value) ? DefaultProfileName : value;
            }

            if (!string.Equals(argument, ProfileOption, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (index + 1 < args.Length)
            {
                var value = args[index + 1]?.Trim();
                if (!string.IsNullOrWhiteSpace(value) && !value.StartsWith("--", StringComparison.Ordinal))
                {
                    return value;
                }
            }

            return DefaultProfileName;
        }

        return DefaultProfileName;
    }
}
