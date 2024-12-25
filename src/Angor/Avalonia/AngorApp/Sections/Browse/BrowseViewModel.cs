using System.Linq;
using AngorApp.Model;
using AngorApp.Services;
using CSharpFunctionalExtensions;
using Zafiro.Avalonia.Controls.Navigation;

namespace AngorApp.Sections.Browse;

public class BrowseViewModel : ReactiveObject, IBrowseViewModel
{
    public BrowseViewModel(Func<Maybe<IWallet>> getWallet, INavigator navigator, UIServices uiServices)
    {
        Projects = SampleData.GetProjects().Select(project => new ProjectViewModel(getWallet, project, navigator, uiServices)).ToList();
        OpenHub = ReactiveCommand.CreateFromTask(() => uiServices.LauncherService.LaunchUri(new Uri("https://www.angor.io")));
    }

    public ReactiveCommand<Unit, Unit> OpenHub { get; set; }

    public IReadOnlyCollection<ProjectViewModel> Projects { get; set; }
}