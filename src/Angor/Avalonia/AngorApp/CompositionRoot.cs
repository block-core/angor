using AngorApp.Sections;
using AngorApp.Sections.Browse;
using AngorApp.Sections.Founder;
using AngorApp.Sections.Home;
using AngorApp.Sections.Portfolio;
using AngorApp.Sections.Shell;
using AngorApp.Sections.Wallet;
using AngorApp.Sections.Wallet.NoWallet;
using AngorApp.Services;
using Avalonia.Controls.Notifications;
using Zafiro.Avalonia.Dialogs;
using Zafiro.Avalonia.Services;
using Separator = AngorApp.Sections.Shell.Separator;
using WalletSectionViewModel = AngorApp.Sections.Wallet.WalletSectionViewModel;

namespace AngorApp;

public class CompositionRoot
{
    public static MainViewModel CreateMainViewModel(Control control)
    {
        var topLevel = TopLevel.GetTopLevel(control);
        var launcher = new LauncherService(topLevel!.Launcher);
        var uiServices = new UIServices(
            launcher, 
            new DesktopDialog(), 
            new NotificationService(new WindowNotificationManager(topLevel)
            {
                Position = NotificationPosition.BottomRight,
            }));

        var walletStoreDesign = new WalletProviderDesign();
        
        IEnumerable<SectionBase> sections =
        [
            new Section("Home", new HomeSectionViewModel(), "svg:/Assets/angor-icon.svg"),
            new Separator(),
            new Section("Wallet", new WalletSectionViewModel(new WalletFactoryDesign(uiServices), walletStoreDesign, uiServices), "fa-wallet"),
            new Section("Browse", new NavigationViewModel(navigator => new BrowseSectionViewModel(walletStoreDesign, navigator, uiServices)), "fa-magnifying-glass"),
            new Section("Portfolio", new PortfolioSectionViewModel(), "fa-hand-holding-dollar"),
            new Section("Founder", new FounderSectionViewModel(), "fa-money-bills"),
            new Separator(),
            new Section("Settings", null, "fa-gear"),
            new CommandSection("Angor Hub", ReactiveCommand.CreateFromTask(() => uiServices.LauncherService.LaunchUri(new Uri("https://www.angor.io"))), "fa-magnifying-glass") { IsPrimary = false }
        ];

        return new MainViewModel(sections, uiServices);
    }
}