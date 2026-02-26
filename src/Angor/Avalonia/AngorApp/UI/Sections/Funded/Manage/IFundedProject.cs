using AngorApp.UI.Sections.Funded.ProjectList.Item;
using AngorApp.UI.Sections.Shared;
using AngorApp.UI.Sections.Shared.Project;

namespace AngorApp.UI.Sections.Funded.Manage;

public interface IFundedProject
{
    IProject Project { get; }
    IInvestmentItem Investment { get; }
}
