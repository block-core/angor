using AngorApp.Sections;
using AngorApp.Sections.Browse;
using AngorApp.Sections.Founder;
using AngorApp.Sections.Home;
using AngorApp.Sections.Portfolio;
using AngorApp.Sections.Shell;
using AngorApp.Sections.Wallet;
using AngorApp.Services;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Templates;
using CSharpFunctionalExtensions;
using Zafiro.Avalonia.Dialogs.Simple;
using Zafiro.Avalonia.Notifications;
using Separator = AngorApp.Sections.Shell.Separator;

namespace AngorApp;

public class CompositionRoot
{
    public static MainViewModel CreateMainViewModel(Control control)
    {
        var topLevel = TopLevel.GetTopLevel(control);
        var launcher = new LauncherService(topLevel!.Launcher);
        var uiServices = new UIServices(launcher, new DesktopDialog(Maybe<DataTemplates>.None), new NotificationService(new WindowNotificationManager(topLevel)));
        
        IEnumerable<SectionBase> sections =
        [
            new Section("Home", new HomeViewModel(), "svg:/Assets/angor-icon.svg"),
            new Separator(),
            new Section("Wallet", new WalletViewModel(), "fa-wallet"),
            new Section("Browse", new NavigationViewModel(navigator => new BrowseViewModel(() => new WalletDesign(), navigator, uiServices)), "fa-magnifying-glass"),
            new Section("Portfolio", new PortfolioViewModel(), "fa-hand-holding-dollar"),
            new Section("Founder", new FounderViewModel(), "fa-money-bills"),
            new Separator(),
            new Section("Settings", new WalletViewModel(), "fa-gear"),
            new CommandSection("Angor Hub", ReactiveCommand.CreateFromTask(() => uiServices.LauncherService.Launch(new Uri("https://www.angor.io"))) , "fa-magnifying-glass") { IsPrimary = false }
        ];
        
        return new MainViewModel(sections, uiServices);
    }
}