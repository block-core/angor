using AngorApp.Model.Funded.Fund.Samples;
using AngorApp.Model.Funded.Investment.Samples;
using AngorApp.Model.ProjectsV2.InvestmentProject;

namespace AngorApp.UI.Sections.FindProjects
{
    public class FindProjectsSectionViewModelSample : IFindProjectsSectionViewModel
    {
        public IEnumerable<IFindProjectItem> Projects { get; set; } = new List<IFindProjectItem>() 
        {
            new FindProjectItemSample()
            {
                Project = new InvestmentProjectSample()
                {
                    IsFundingOpen = Observable.Return(true),
                    IsFundingFailed = Observable.Return(false),
                    IsFundingSuccessful = Observable.Return(false),
                    InvestorCount = Observable.Return(123),
                    Target = new AmountUI(123456),
                    Raised = Observable.Return(new AmountUI(12345)),
                },
            },
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
