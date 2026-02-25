using Angor.Sdk.Funding.Shared;
using Angor.Shared.Models;
using AngorApp.Model.ProjectsV2.FundProject;
using ProjectStatus = AngorApp.Model.ProjectsV2.ProjectStatus;

namespace AngorApp.Model.Funded.Fund.Samples
{
    public class FundProjectSample : IFundProject
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
        public IAmountUI Goal { get; set; } = AmountUI.FromBtc(2m);
        public IObservable<IAmountUI> Funded { get; } = Observable.Return<IAmountUI>(new AmountUI(30));
        public IObservable<IReadOnlyCollection<IPayment>> Payments { get; } = Observable.Return<IReadOnlyCollection<IPayment>>(new[] { new Model.Payment() });
        public IObservable<int> FunderCount { get; } = Observable.Return(45);
        public IObservable<int> SupporterCount { get; } = Observable.Return(45);
        public IAmountUI FundingTarget => Goal;
        public IObservable<IAmountUI> FundingRaised => Funded;
        public IObservable<ProjectStatus> ProjectStatus { get; } = Observable.Return(AngorApp.Model.ProjectsV2.ProjectStatus.Open);

        public DateTimeOffset FundingStart { get; } = DateTimeOffset.Now.AddDays(-14);
        public DateTimeOffset FundingEnd { get; init; } = DateTimeOffset.Now.AddDays(30);
        public DateTimeOffset TransactionDate { get; set; } = DateTimeOffset.Now.AddDays(-1);
        public FundingStatus Status { get; set; } = FundingStatus.Waiting;
        public IReadOnlyList<DynamicStagePattern> DynamicStagePatterns { get; } = new List<DynamicStagePattern>().AsReadOnly();
    }
}