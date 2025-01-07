using System.Linq;
using AngorApp.Sections.Shell;
using AngorApp.Sections.Wallet;
using AngorApp.Sections.Wallet.NoWallet;
using AngorApp.Services;

namespace AngorApp.Sections.Browse;

public class BrowseSectionViewModelDesign : IBrowseSectionViewModel
{
    public BrowseSectionViewModelDesign()
    {
        Projects = SampleData.GetProjects().Select(project => new ProjectViewModel(new WalletProviderDesign(), project, null, new UIServices(new NoopLauncherService(), new TestDialog(), new TestNotificationService()))).ToList();
    }
    
    public IReadOnlyCollection<ProjectViewModel> Projects { get; set; }
    public ReactiveCommand<Unit, Unit> OpenHub { get; set; }
}