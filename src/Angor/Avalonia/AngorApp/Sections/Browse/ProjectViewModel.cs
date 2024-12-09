using AngorApp.Sections.Browse.Details;
using Zafiro.Avalonia.Controls.Navigation;

namespace AngorApp.Sections.Browse;

public class ProjectViewModel : ReactiveObject
{
    private readonly Project project;

    public ProjectViewModel(Project project, INavigator navigator)
    {
        this.project = project;
        GoToDetails = ReactiveCommand.Create(() => navigator.Go(() => new ProjectDetailsViewModel(project)));
    }

    public string Name => project.Name;
    public string ShortDescription => project.ShortDescription;
    public object Icon => project.Icon;
    public object Picture => project.Picture;

    public ReactiveCommand<Unit, Unit> GoToDetails { get; set; }
}