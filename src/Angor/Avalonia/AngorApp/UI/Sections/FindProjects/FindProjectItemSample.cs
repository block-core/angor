namespace AngorApp.UI.Sections.FindProjects
{
    public class FindProjectItemSample : IFindProjectItem
    {
        public string Name { get; set; } = string.Empty;
        public IAmountUI FundingTarget { get; set; } = null!;
        public IAmountUI? FundingRaised { get; set; }
        public string Description { get; set; } = string.Empty;
        public int? InvestorsCount { get; set; }
        public Uri BannerUrl { get; set; } = null!;
        public Uri LogoUrl { get; set; } = null!;
        public IEnhancedCommand GoToDetails { get; } = null!;
        public IEnhancedCommand LoadStatistics { get; } = null!;
    }
}