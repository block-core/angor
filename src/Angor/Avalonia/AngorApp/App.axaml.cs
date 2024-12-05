using Avalonia;
using Avalonia.Markup.Xaml;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.FontAwesome;
using Zafiro.Avalonia.Mixins;
using MainView = AngorApp.Sections.Shell.MainView;
using MainWindow = AngorApp.Sections.Shell.MainWindow;

namespace AngorApp;

public partial class App : Application
{
    public override void Initialize()
    {
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