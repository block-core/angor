namespace AngorApp.Sections.Founder.ManageFunds;

public class ProjectViewModel : IProjectViewModel
{
    public required string Banner { get; init; }
    public required  string Avatar { get; init; }
    public required  string Name { get; init; }
    public required  string ShortDescription { get; init; }
}