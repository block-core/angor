using AngorApp.UI.Sections.FundedV2.Common.Model;

namespace AngorApp.UI.Sections.FundedV2.Host.Section;

public interface IFundedSectionViewModel
{
    public IEnhancedCommand FindProjects { get; }
    public IReadOnlyCollection<IFundedItem> FundedItems { get; }
    public IEnhancedCommand Refresh { get; }
}