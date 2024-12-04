using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AngorApp.Sections.Browse;
using AngorApp.Sections.Founder;
using AngorApp.Sections.Home;
using AngorApp.Sections.Shell;
using AngorApp.Sections.Wallet;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AngorApp.Views;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.FontAwesome;
using ReactiveUI;
using Zafiro.Avalonia.Mixins;
using MainViewModel = AngorApp.Sections.Shell.MainViewModel;
using Separator = AngorApp.Sections.Shell.Separator;

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
        
        this.Connect(() => new MainView(), control => CreateMainViewModel(control), () => new MainWindow());

        base.OnFrameworkInitializationCompleted();
    }

    private static MainViewModel CreateMainViewModel(Control control)
    {
        var topLevel = TopLevel.GetTopLevel(control);
        var launcher = new LauncherService(topLevel!.Launcher);
        var uiServices = new UIServices(launcher);
        
        IEnumerable<SectionBase> sections =
        [
            new Section("Home", new HomeViewModel(), "svg:/Assets/angor-icon.svg"),
            new Separator(),
            new Section("Wallet", new WalletViewModel(), "fa-wallet"),
            new Section("Browse", new BrowseViewModel(uiServices), "fa-magnifying-glass"),
            new Section("Portfolio", new WalletViewModel(), "fa-hand-holding-dollar"),
            new Section("Founder", new FounderViewModel(), "fa-money-bills"),
            new Separator(),
            new Section("Settings", new WalletViewModel(), "fa-gear"),
            new CommandSection("Angor Hub", ReactiveCommand.CreateFromTask(() => uiServices.LauncherService.Launch(new Uri("https://www.angor.io"))) , "fa-magnifying-glass") { IsPrimary = false }
        ];
        
        return new MainViewModel(sections, uiServices);
    }
}

public class UIServices
{
    public ILauncherService LauncherService { get; }

    public UIServices(ILauncherService launcherService)
    {
        LauncherService = launcherService;
    }
}

public interface ILauncherService
{
    Task Launch(Uri uri);
}

public class NoopLauncherService : ILauncherService
{
    public Task Launch(Uri uri)
    {
        return Task.CompletedTask;
    }
}

public class LauncherService : ILauncherService
{
    private readonly ILauncher launcher;

    public LauncherService(ILauncher launcher)
    {
        this.launcher = launcher;
    }

    public Task Launch(Uri uri) => launcher.LaunchUriAsync(uri);
}