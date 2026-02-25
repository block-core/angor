using AngorApp.UI.Sections.Funded.ProjectList.Item;
using AngorApp.UI.Sections.Shared;

namespace AngorApp.UI.Sections.Funded.Manage;

public interface IFundedProject
{
    IProjectItem Project { get; }
    IInvestmentItem Investment { get; }
}
