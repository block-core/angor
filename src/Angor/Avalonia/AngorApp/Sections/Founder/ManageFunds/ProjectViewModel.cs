namespace AngorApp.Sections.Founder.ManageFunds;

public interface IProjectViewModel
{
    string Banner { get; }
    string Avatar { get; }
    string Name { get; }
    string ShortDescription { get; }
}

public class ProjectViewModelDesign : IProjectViewModel
{
    public string Banner { get; set; } = "avares://AngorApp/Assets/background.jpg";
    public string Avatar { get; set; } = "avares://AngorApp/Assets/star.jpg";
    public string Name { get; set; } = "Sample Project";
    public string ShortDescription { get; set; } = "This is a sample project description for design purposes.";
}

public class ProjectViewModel : IProjectViewModel
{
    public required string Banner { get; init; }
    public required  string Avatar { get; init; }
    public required  string Name { get; init; }
    public required  string ShortDescription { get; init; }
}