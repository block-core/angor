namespace AngorApp.UI.Sections.Founder.ProjectDetails.MainView;

public interface IProjectViewModel
{
    Uri? Banner { get; }
    Uri? Avatar { get; }
    string Name { get; }
    string ShortDescription { get; }
}