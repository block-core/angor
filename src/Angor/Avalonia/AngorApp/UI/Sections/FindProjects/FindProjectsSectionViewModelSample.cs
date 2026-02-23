namespace AngorApp.UI.Sections.FindProjects
{
    public class FindProjectsSectionViewModelSample : IFindProjectsSectionViewModel
    {
        public IEnumerable<IFindProjectItem> Projects { get; set; } = new List<IFindProjectItem>() {
            new FindProjectItemSample() {
                Description = "Sample project description",
                InvestorsCount = 123,
                FundingRaised = new AmountUI(20000000),
                FundingTarget = new AmountUI(100000000),
                Name = "Sample project name",
                BannerUrl = new Uri("https://www.nostria.app/assets/nostria-social.jpg"),
                LogoUrl = new Uri("https://www.nostria.app/assets/icons/icon-512x512-margin.png"),
            },
            new FindProjectItemSample() {
                Description = "Sample project description",
                InvestorsCount = 123,
                FundingRaised = new AmountUI(20000000),
                FundingTarget = new AmountUI(100000000),
                Name = "Sample project name",
                BannerUrl = new Uri("https://www.nostria.app/assets/nostria-social.jpg"),
                LogoUrl = new Uri("https://www.nostria.app/assets/icons/icon-512x512-margin.png"),
            },
            new FindProjectItemSample() {
                Description = "Sample project description",
                InvestorsCount = 123,
                FundingRaised = new AmountUI(20000000),
                FundingTarget = new AmountUI(100000000),
                Name = "Sample project name",
                BannerUrl = new Uri("https://www.nostria.app/assets/nostria-social.jpg"),
                LogoUrl = new Uri("https://www.nostria.app/assets/icons/icon-512x512-margin.png"),
            },
            new FindProjectItemSample() {
                Description = "Sample project description",
                InvestorsCount = 123,
                FundingRaised = new AmountUI(20000000),
                FundingTarget = new AmountUI(100000000),
                Name = "Sample project name",
                BannerUrl = new Uri("https://www.nostria.app/assets/nostria-social.jpg"),
                LogoUrl = new Uri("https://www.nostria.app/assets/icons/icon-512x512-margin.png"),
            },
            new FindProjectItemSample() {
                Description = "Sample project description",
                InvestorsCount = 123,
                FundingRaised = new AmountUI(20000000),
                FundingTarget = new AmountUI(100000000),
                Name = "Sample project name",
                BannerUrl = new Uri("https://www.nostria.app/assets/nostria-social.jpg"),
                LogoUrl = new Uri("https://www.nostria.app/assets/icons/icon-512x512-margin.png"),
            }
        };

        public IEnumerable<SortOption> SortOptions { get; } = [
            new("All", 18),
            new("Open", 10),
            new("Closed", 6),
            new("Funded", 2)
        ];

        public IEnhancedCommand<Result<IEnumerable<FindProjectItem>>> LoadProjects { get; set; } = null!;
    }
}