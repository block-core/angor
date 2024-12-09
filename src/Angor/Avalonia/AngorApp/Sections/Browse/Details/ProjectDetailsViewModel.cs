namespace AngorApp.Sections.Browse.Details;

public class ProjectDetailsViewModel(Project project) : ReactiveObject, IProjectDetailsViewModel
{
    public string Name => project.Name;
    public string ShortDescription => project.ShortDescription;
    public object Icon => project.Icon;
    public object Picture => project.Picture;
}