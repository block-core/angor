namespace AngorApp.UI.Sections.Founder.CreateProject.Preview;

public class PreviewHeaderViewModel : ReactiveObject
{
    public CreateProjectViewModel CreateProject { get; }

    public PreviewHeaderViewModel(CreateProjectViewModel createProject)
    {
        CreateProject = createProject;
    }
}