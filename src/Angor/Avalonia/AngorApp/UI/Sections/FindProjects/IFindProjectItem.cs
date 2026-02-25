using AngorApp.Model.ProjectsV2;

namespace AngorApp.UI.Sections.FindProjects
{
    public interface IFindProjectItem
    {
        IProject Project { get; }
        IEnhancedCommand GoToDetails { get; }
    }
}
