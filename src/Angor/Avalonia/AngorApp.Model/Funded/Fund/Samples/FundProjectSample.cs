using Angor.Sdk.Funding.Shared;
using AngorApp.Model.ProjectsV2.FundProject;

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
        public IEnhancedCommand<Result> Invest { get; } = EnhancedCommand.CreateWithResult(Result.Success);
        public IEnhancedCommand ManageFunds { get; } = EnhancedCommand.Create(() => { }, Observable.Return(false), text: "Manage Funds");
        public IEnhancedCommand Refresh { get; } = EnhancedCommand.Create(() => { });
        public IAmountUI Goal { get; set; } = AmountUI.FromBtc(2m);
        public IObservable<IAmountUI> Funded { get; } = Observable.Return(AmountUI.FromBtc(1.5m));
        public IObservable<IReadOnlyCollection<IPayment>> Payments { get; } = Observable.Return<IReadOnlyCollection<IPayment>>(new[] { new Model.Payment() });
        public IObservable<int> FunderCount { get; } = Observable.Return(45);
        public DateTimeOffset TransactionDate { get; set; } = DateTimeOffset.Now.AddDays(-1);
    }
}
