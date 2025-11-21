namespace AngorApp.UI.Sections.Home;

public interface IHomeSectionViewModel
{
    IEnhancedCommand FindProjects { get; set; }
    IEnhancedCommand CreateProject { get; set; }
}