using AngorApp.UI.Sections.Shared;
using AngorApp.UI.Sections.Shared.Project;

namespace AngorApp.UI.Sections.MyProjects.Items
{
    public interface IMyProjectItem
    {
        IProject Project { get; }
        IEnhancedCommand ManageFunds { get; }
    }
}
