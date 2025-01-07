using System.Linq;
using AngorApp.Sections.Wallet.NoWallet;
using AngorApp.Services;
using Zafiro.Avalonia.Controls.Navigation;

namespace AngorApp.Sections.Browse;

public class BrowseSectionViewModel : ReactiveObject, IBrowseSectionViewModel
{
    public BrowseSectionViewModel(IWalletProvider walletProvider, INavigator navigator, UIServices uiServices)
    {
        Projects = SampleData.GetProjects().Select(project => new ProjectViewModel(walletProvider, project, navigator, uiServices)).ToList();
        OpenHub = ReactiveCommand.CreateFromTask(() => uiServices.LauncherService.LaunchUri(new Uri("https://www.angor.io")));
    }

    public ReactiveCommand<Unit, Unit> OpenHub { get; set; }

    public IReadOnlyCollection<ProjectViewModel> Projects { get; set; }
}