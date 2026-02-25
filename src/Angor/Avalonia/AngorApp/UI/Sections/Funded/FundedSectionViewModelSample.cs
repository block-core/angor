using AngorApp.UI.Sections.Funded.ProjectList.Item;

namespace AngorApp.UI.Sections.Funded;

public class FundedSectionViewModelSample : IFundedSectionViewModel
{
    public IEnhancedCommand FindProjects { get; }
    public IReadOnlyCollection<IFundedProjectItem> FundedProjects { get; set; } = [];
    public IEnhancedCommand Refresh { get; }
}