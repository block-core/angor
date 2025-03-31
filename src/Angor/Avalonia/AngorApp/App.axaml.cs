using AngorApp.Composition;
using AngorApp.Core;
using AngorApp.Sections.Shell;
using Avalonia;
using Avalonia.Markup.Xaml;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.FontAwesome;
using Serilog;
using Zafiro.Avalonia.Mixins;

namespace AngorApp;

public partial class App : Application
{
    public override void Initialize()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Debug()
            .CreateLogger();

        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        IconProvider.Current
            .Register<FontAwesomeIconProvider>();

        this.Connect(() => new MainView(), CompositionRoot.CreateMainViewModel, () => new MainWindow());

        base.OnFrameworkInitializationCompleted();
    }
}