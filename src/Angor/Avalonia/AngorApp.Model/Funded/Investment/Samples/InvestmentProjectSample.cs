using Angor.Sdk.Funding.Shared;
using AngorApp.Model.ProjectsV2.InvestmentProject;
using IStage = AngorApp.Model.ProjectsV2.InvestmentProject.IStage;

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
        public IEnhancedCommand<Result> Invest { get; } = EnhancedCommand.CreateWithResult(Result.Success);
        public IEnhancedCommand ManageFunds { get; } = EnhancedCommand.Create(() => { });
        public IEnhancedCommand Refresh { get; } = EnhancedCommand.Create(() => { });
        public IAmountUI Target { get; set; } = new AmountUI(100_000_000_000);
        public IObservable<IAmountUI> Raised { get; set; } = Observable.Return(new AmountUI(24_000_000_000));
        public IObservable<IAmountUI> TotalInvestment { get; set; } = Observable.Return(new AmountUI(24_000_000_000));
        public IObservable<IAmountUI> AvailableBalance { get; set; } = Observable.Return(new AmountUI(15_000_000_000));
        public IObservable<IAmountUI> Withdrawable { get; set; } = Observable.Return(new AmountUI(8_000_000_000));
        public IObservable<int> TotalStages { get; set; } = Observable.Return(4);
        public IObservable<int> InvestorCount { get; set; } = Observable.Return(120);
        public IObservable<IReadOnlyCollection<IStage>> Stages { get; } = Observable.Return(new[] { new StageSample() });
        public DateTimeOffset FundingStart { get; } = DateTimeOffset.Now.AddDays(-60);
        public DateTimeOffset FundingEnd { get; } = DateTimeOffset.Now.AddDays(60);
        public IObservable<bool> IsFundingOpen { get; set; } = Observable.Return(true);
        public IObservable<bool> IsFundingSuccessful { get; set; } = Observable.Return(false);
        public IObservable<bool> IsFundingFailed { get; set; } = Observable.Return(false);
        public TimeSpan PenaltyDuration { get; } = TimeSpan.FromDays(30);
        public IAmountUI? PenaltyThreshold { get; } = new AmountUI(1_000_000);
    }
}
