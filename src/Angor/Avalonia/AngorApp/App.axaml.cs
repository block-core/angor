using System.IO;
using AngorApp.Composition;
using AngorApp.Sections.Shell;
using Avalonia;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Configuration;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.FontAwesome;
using Projektanker.Icons.Avalonia.MaterialDesign;
using Serilog;
using Zafiro.Avalonia.Icons;
using Zafiro.Avalonia.Misc;
using Humanizer.Configuration;
using AngorApp.Localization;

namespace AngorApp;

public partial class App : Application
{
    public override void Initialize()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .MinimumLevel.Debug()
            .CreateLogger();

        // Register Humanizer strategy to prefer "in X" over "X from now" in English
        Configurator.DateTimeHumanizeStrategy = new InPrepositionDateTimeHumanizeStrategy();

        AvaloniaXamlLoader.Load(this);
    }
    
    public override void OnFrameworkInitializationCompleted()
    {
        IconProvider.Current
            .Register<FontAwesomeIconProvider>()
            .Register<MaterialDesignIconProvider>()
            .RegisterPathStringIconProvider("path");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
        
        this.Connect(() => new MainView(),x => CompositionRoot.CreateMainViewModel(x,configuration), () => new MainWindow());

        base.OnFrameworkInitializationCompleted();
    }
}