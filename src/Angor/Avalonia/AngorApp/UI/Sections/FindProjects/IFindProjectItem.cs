namespace AngorApp.UI.Sections.FindProjects
{
    public interface IFindProjectItem
    {
        public string Name { get; }
        IAmountUI FundingTarget { get; }
        IAmountUI? FundingRaised { get; }
        string Description { get; }
        int? InvestorsCount { get; }
        Uri BannerUrl { get; }
        Uri LogoUrl { get; }
        IEnhancedCommand GoToDetails { get; }
        IEnhancedCommand LoadStatistics { get; }
    }
}