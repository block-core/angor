using Angor.Sdk.Funding.Projects.Dtos;
using Angor.Sdk.Funding.Shared;

namespace AngorApp.Model.ProjectsV2
{
    public abstract class Project : IProject
    {
        protected Project(ProjectDto seed, IEnhancedCommand<Result> invest, IEnhancedCommand manageFunds)
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
            ManageFunds = manageFunds;
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
        public IEnhancedCommand ManageFunds { get; }
        public abstract IEnhancedCommand Refresh { get; }
        public abstract IAmountUI FundingTarget { get; }
        public abstract IObservable<IAmountUI> FundingRaised { get; }
        public abstract IObservable<int> SupporterCount { get; }

        protected static IEnhancedCommand CreateUnsupportedManageFundsCommand()
        {
            return EnhancedCommand.Create(
                () => throw new NotSupportedException("Manage funds is not supported for this project type."),
                Observable.Return(false), text: "N/A");
        }
    }
}
