using System.Linq;
using AngorApp.Services;
using Zafiro.Avalonia.Controls.Navigation;

namespace AngorApp.Sections.Browse;

public class BrowseViewModel : ReactiveObject, IBrowseViewModel
{
    public BrowseViewModel(INavigator navigator, UIServices uiServices)
    {
        Projects = SampleData.GetProjects().Select(project => new ProjectViewModel(project, navigator)).ToList();

        OpenHub = ReactiveCommand.CreateFromTask(() => uiServices.LauncherService.Launch(new Uri("https://www.angor.io")));
    }

    public ReactiveCommand<Unit, Unit> OpenHub { get; set; }

    public IReadOnlyCollection<ProjectViewModel> Projects { get; set; }
}