using AngorApp.Model.Funded.Fund.Samples;

namespace AngorApp.UI.Sections.FindProjects
{
    public class FindProjectsSectionViewModelSample : IFindProjectsSectionViewModel
    {
        public IEnumerable<IFindProjectItem> Projects { get; set; } = new List<IFindProjectItem>() {
            new FindProjectItemSample(),
            new FindProjectItemSample() { Project = new FundProjectSample() },
            new FindProjectItemSample(),
            new FindProjectItemSample() { Project = new FundProjectSample() },
            new FindProjectItemSample()
        };

        public IEnumerable<SortOption> SortOptions { get; } = [
            new("All", 18),
            new("Open", 10),
            new("Closed", 6),
            new("Funded", 2)
        ];

        public IEnhancedCommand<Result<IEnumerable<FindProjectItem>>> LoadProjects { get; set; }
    }
}
