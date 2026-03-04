using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Dtos;
using Angor.Sdk.Funding.Shared;

namespace AngorApp.Model.ProjectsV2
{
    public abstract class Project : IProject
    {
        protected Project(ProjectDto seed, IEnhancedCommand<Result> invest)
        {
            Name = seed.Name;
            Description = seed.ShortDescription;
            BannerUrl = seed.Banner;
            LogoUrl = seed.Avatar;
            Id = seed.Id;
            FounderPubKey = seed.FounderPubKey;
            NostrNpubKeyHex = seed.NostrNpubKeyHex;
            InformationUri = seed.InformationUri;
            Invest = invest;
        }

        public string Name { get; }
        public string Description { get; }
        public Uri? BannerUrl { get; }
        public Uri? LogoUrl { get; }
        public ProjectId Id { get; }
        public string FounderPubKey { get; }
        public string NostrNpubKeyHex { get; }
        public Uri? InformationUri { get; }
        public IEnhancedCommand<Result> Invest { get; }
        public abstract IEnhancedCommand Refresh { get; }
        public abstract IAmountUI FundingTarget { get; }
        public abstract IObservable<IAmountUI> FundingRaised { get; }
        public abstract IObservable<int> SupporterCount { get; }

        public static IProject Create(ProjectDto seed, IProjectAppService projectAppService, IEnhancedCommand<Result> invest)
        {
            return seed.ProjectType switch
            {
                Angor.Shared.Models.ProjectType.Invest => new InvestmentProject.InvestmentProject(seed, projectAppService, invest),
                Angor.Shared.Models.ProjectType.Fund => new FundProject.FundProject(seed, projectAppService, invest),
                _ => throw new ArgumentOutOfRangeException(nameof(seed.ProjectType), "Unsupported project type")
            };
        }
    }
}
