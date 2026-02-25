using AngorApp.UI.Sections.Funded.Manage;
using AngorApp.UI.Sections.Shared;

namespace AngorApp.UI.Sections.Funded.ProjectList.Item;

public interface IFundedProjectItem : IFundedProject
{
    IEnhancedCommand Manage { get; }
}