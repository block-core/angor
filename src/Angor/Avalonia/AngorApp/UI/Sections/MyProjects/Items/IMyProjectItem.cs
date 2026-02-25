using AngorApp.Model.ProjectsV2;

namespace AngorApp.UI.Sections.MyProjects.Items
{
    public interface IMyProjectItem
    {
        IProject Project { get; }
        IEnhancedCommand ManageFunds { get; }
    }
}
