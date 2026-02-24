using AngorApp.UI.Sections.Shared;

namespace AngorApp.UI.Sections.MyProjects.Items
{
    public interface IMyProjectItem
    {
        IProjectItem Project { get; }
        IEnhancedCommand ManageFunds { get; }
    }
}
