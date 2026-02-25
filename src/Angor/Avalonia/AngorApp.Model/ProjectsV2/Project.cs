using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Dtos;
using Angor.Sdk.Funding.Shared;

namespace AngorApp.Model.ProjectsV2
{
    public abstract class Project : IProject
    {
        protected Project(ProjectDto seed)
        {
            Name = seed.Name;
            Description = seed.ShortDescription;
            BannerUrl = seed.Banner;
            LogoUrl = seed.Avatar;
            Id = seed.Id;
            FounderPubKey = seed.FounderPubKey;
            NostrNpubKeyHex = seed.NostrNpubKeyHex;
            InformationUri = seed.InformationUri;
            FundingStart = seed.FundingStartDate;
            FundingEnd = seed.FundingEndDate;
        }

        public string Name { get; }
        public string Description { get; }
        public Uri? BannerUrl { get; }
        public Uri? LogoUrl { get; }
        public ProjectId Id { get; }
        public string FounderPubKey { get; }
        public string NostrNpubKeyHex { get; }
        public Uri? InformationUri { get; }
        public DateTimeOffset FundingStart { get; }
        public DateTimeOffset FundingEnd { get; }
        public abstract IEnhancedCommand Refresh { get; }
        public abstract IObservable<ProjectStatus> ProjectStatus { get; }
        public abstract IAmountUI FundingTarget { get; }
        public abstract IObservable<IAmountUI> FundingRaised { get; }
        public abstract IObservable<int> SupporterCount { get; }

        public static IProject Create(ProjectDto seed, IProjectAppService projectAppService)
        {
            return seed.ProjectType switch
            {
                Angor.Shared.Models.ProjectType.Invest => new InvestmentProject.InvestmentProject(seed, projectAppService),
                Angor.Shared.Models.ProjectType.Fund => new FundProject.FundProject(seed, projectAppService),
                _ => throw new ArgumentOutOfRangeException(nameof(seed.ProjectType), "Unsupported project type")
            };
        }
    }
}