using AngorApp.UI.Sections.Funded.Manage;
using AngorApp.UI.Sections.FundedV2.Manage;

namespace AngorApp.UI.Sections.Funded.Section;

public interface IFundedSectionViewModel
{
    public IEnhancedCommand FindProjects { get; }
    public IReadOnlyCollection<IFundedItem2> FundedItems { get; }
    public IEnhancedCommand Refresh { get; }
}