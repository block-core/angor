using AngorApp.Model.Funded.Shared.Model;

namespace AngorApp.UI.Sections.Funded.Shared.Section;

public interface IFundedSectionViewModel
{
    public IEnhancedCommand FindProjects { get; }
    public IReadOnlyCollection<IFundedItem> FundedItems { get; }
    public IEnhancedCommand Refresh { get; }
}