namespace AngorApp.Sections.Browse;
    

public class ProjectViewModelSample : IProjectViewModel
{
    public ProjectViewModelSample()
    {
    }
    
    public ProjectViewModelSample(IProject project)
    {
        Project = project;
    }

    public IProject Project { get; set; }
    public IEnhancedCommand<Result> GoToDetails { get; set; }
}