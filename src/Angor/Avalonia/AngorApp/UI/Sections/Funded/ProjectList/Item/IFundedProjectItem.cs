using AngorApp.UI.Sections.Shared;

namespace AngorApp.UI.Sections.Funded.ProjectList.Item
{
    public interface IFundedProjectItem
    {
        IProjectItem Project { get; }
        IEnhancedCommand Manage { get; }
    }
}