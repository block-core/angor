using Angor.Sdk.Funding.Shared;
using AngorApp.Model.ProjectsV2.InvestmentProject;
using IStage = AngorApp.Model.ProjectsV2.InvestmentProject.IStage;
using ProjectStatus = AngorApp.Model.ProjectsV2.ProjectStatus;

namespace AngorApp.Model.Funded.Investment.Samples
{
    public class InvestmentProjectSample : IInvestmentProject
    {
        public string Name { get; } = "Sample";
        public string Description { get; } = "Description";
        public Uri? BannerUrl { get; } = new Uri("https://images-assets.nasa.gov/image/PIA12235/PIA12235~thumb.jpg");
        public Uri? LogoUrl { get; } = new Uri("https://images-assets.nasa.gov/image/PIA12235/PIA12235~thumb.jpg");
        public ProjectId Id { get; } = new ProjectId("test");
        public string FounderPubKey { get; } = "ca6e84aa974d00af805a754b34bc4e3c9a899aac14487a6f2e21fe9ea4b9fe43";
        public string NostrNpubKeyHex { get; } = "ca6e84aa974d00af805a754b34bc4e3c9a899aac14487a6f2e21fe9ea4b9fe43";
        public Uri? InformationUri { get; } = null;
        public IEnhancedCommand Refresh { get; } = EnhancedCommand.Create(() => { });
        public IAmountUI Target { get; } = new AmountUI(100_000_000_000);
        public IObservable<IAmountUI> Raised { get; } = Observable.Return(new AmountUI(24_000_000_000));
        public IObservable<int> InvestorCount { get; } = Observable.Return(120);
        public IObservable<int> SupporterCount { get; } = Observable.Return(120);
        public IAmountUI FundingTarget => Target;
        public IObservable<IAmountUI> FundingRaised => Raised;
        public IObservable<IReadOnlyCollection<IStage>> Stages { get; } = Observable.Return(new[] { new StageSample() });
        public DateTimeOffset FundingStart { get; } = DateTimeOffset.Now.AddDays(-60);
        public DateTimeOffset FundingEnd { get; } = DateTimeOffset.Now.AddDays(60);
        public TimeSpan PenaltyDuration { get; } = TimeSpan.FromDays(30);
        public IAmountUI? PenaltyThreshold { get; } = new AmountUI(1_000_000);
        public IObservable<ProjectStatus> ProjectStatus { get; } = Observable.Return(AngorApp.Model.ProjectsV2.ProjectStatus.Open);

    }
}
