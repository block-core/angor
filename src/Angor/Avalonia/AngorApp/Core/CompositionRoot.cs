using System.Linq;
using System.Threading.Tasks;
using Angor.Model.Implementation;
using Angor.Model.Implementation.Projects;
using AngorApp.Design;
using AngorApp.Model;
using AngorApp.Sections;
using AngorApp.Sections.Browse;
using AngorApp.Sections.Founder;
using AngorApp.Sections.Home;
using AngorApp.Sections.Portfolio;
using AngorApp.Sections.Shell;
using AngorApp.Sections.Wallet;
using AngorApp.Sections.Wallet.CreateAndRecover;
using AngorApp.Services;
using Avalonia.Controls.Notifications;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Zafiro.Avalonia.Dialogs;
using Zafiro.Avalonia.Services;
using Separator = AngorApp.Sections.Shell.Separator;

namespace AngorApp.Core;

public static class CompositionRoot
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
                Position = NotificationPosition.BottomRight
            }));

        var walletStoreDesign = new WalletProviderDesign();
        var walletFactory = new WalletFactory(new WalletBuilderDesign(), uiServices);

        MainViewModel mainViewModel = null!;

        var projectService = RealProjectService();

        IEnumerable<SectionBase> sections =
        [
            new Section("Home", new HomeSectionViewModel(walletStoreDesign, uiServices, () => mainViewModel), "svg:/Assets/angor-icon.svg"),
            new Separator(),
            new Section("Wallet", new WalletSectionViewModel(walletFactory, walletStoreDesign, uiServices), "fa-wallet"),
            new Section("Browse", new NavigationViewModel(navigator => new BrowseSectionViewModel(walletStoreDesign, projectService, navigator, uiServices)), "fa-magnifying-glass"),
            new Section("Portfolio", new PortfolioSectionViewModel(), "fa-hand-holding-dollar"),
            new Section("Founder", new FounderSectionViewModel(projectService), "fa-money-bills"),
            new Separator(),
            new Section("Settings", null, "fa-gear"),
            new CommandSection("Angor Hub", ReactiveCommand.CreateFromTask(() => uiServices.LauncherService.LaunchUri(Constants.AngorHubUri)), "fa-magnifying-glass") { IsPrimary = false }
        ];

        mainViewModel = new MainViewModel(sections, uiServices);

        return mainViewModel;
    }

    private static ProjectService RealProjectService()
    {
        var loggerFactory = LoggerConfig.CreateFactory();
        return new ProjectService(DependencyFactory.GetIndexerService(loggerFactory), DependencyFactory.GetRelayService(loggerFactory));
    }
}