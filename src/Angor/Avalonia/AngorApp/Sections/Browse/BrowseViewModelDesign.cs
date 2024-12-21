using System.Linq;
using System.Threading.Tasks;
using AngorApp.Sections.Shell;
using AngorApp.Sections.Wallet;
using AngorApp.Services;
using CSharpFunctionalExtensions;
using Zafiro.UI;

namespace AngorApp.Sections.Browse;

public class BrowseViewModelDesign : IBrowseViewModel
{
    public BrowseViewModelDesign()
    {
        Projects = SampleData.GetProjects().Select(project => new ProjectViewModel(() => new WalletDesign(), project, null, new UIServices(new NoopLauncherService(), new TestDialog(), new TestNotificationService()))).ToList();
    }
    
    public IReadOnlyCollection<ProjectViewModel> Projects { get; set; }
    public ReactiveCommand<Unit, Unit> OpenHub { get; set; }
}

public class TestNotificationService : INotificationService
{
    public Task Show(string message, Maybe<string> title)
    {
        return Task.CompletedTask;
    }
}
