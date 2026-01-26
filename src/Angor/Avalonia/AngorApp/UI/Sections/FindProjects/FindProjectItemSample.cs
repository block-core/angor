namespace AngorApp.UI.Sections.FindProjects
{
    public class FindProjectItemSample : IFindProjectItem
    {
        public string Name { get; set; }
        public IAmountUI FundingTarget { get; set; }
        public IAmountUI? FundingRaised { get; set; }
        public string Description { get; set; }
        public int? InvestorsCount { get; set; }
        public Uri BannerUrl { get; set; }
        public Uri LogoUrl { get; set; }
        public IEnhancedCommand GoToDetails { get; } = null!;
        public IEnhancedCommand LoadStatistics { get; } = null!;
    }
}