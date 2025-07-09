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
    public string Banner { get; set; }
    public string Avatar { get; set; }
    public string Name { get; set; }
    public string ShortDescription { get; set; }
}

public class ProjectViewModel : IProjectViewModel
{
    public required string Banner { get; init; }
    public required  string Avatar { get; init; }
    public required  string Name { get; init; }
    public required  string ShortDescription { get; init; }
}