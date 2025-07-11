namespace AngorApp.Sections.Founder.ManageFunds;

public class ProjectViewModelDesign : IProjectViewModel
{
    public string Banner { get; set; } = "avares://AngorApp/Assets/background.jpg";
    public string Avatar { get; set; } = "avares://AngorApp/Assets/star.jpg";
    public string Name { get; set; } = "Sample Project";
    public string ShortDescription { get; set; } = "This is a sample project description for design purposes.";
}