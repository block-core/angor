namespace AngorApp.Sections.Founder.ProjectDetails.MainView;

public class ProjectViewModelDesign : IProjectViewModel
{
    public Uri? Banner { get; set; } = new("avares://AngorApp/Assets/background.jpg");
    public Uri? Avatar { get; set; } = new("avares://AngorApp/Assets/star.jpg");
    public string Name { get; set; } = "Sample Project";
    public string ShortDescription { get; set; } = "This is a sample project description for design purposes.";
}