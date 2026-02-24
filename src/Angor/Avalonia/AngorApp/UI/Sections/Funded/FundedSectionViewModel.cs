using AngorApp.UI.Sections.Funded.ProjectList.Item;

namespace AngorApp.UI.Sections.Funded
{
    public class FundedSectionViewModel : IFundedSectionViewModel
    {
        public IEnhancedCommand FindProjects { get; }
        public IReadOnlyCollection<IFundedProjectItem> FundedProjects { get; } = [];
        public IEnhancedCommand LoadProjects { get; }
    }
}