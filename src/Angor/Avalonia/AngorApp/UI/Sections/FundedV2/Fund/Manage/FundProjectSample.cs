using Angor.Sdk.Funding.Shared;
using AngorApp.UI.Sections.Shared.ProjectV2;

namespace AngorApp.UI.Sections.FundedV2.Fund.Manage
{
    public class FundProjectSample : IFundProject
    {
        public string Name { get; } = "Sample";
        public string Description { get; } = "Description";
        public Uri? BannerUrl { get; } = new Uri("https://images-assets.nasa.gov/image/PIA12235/PIA12235~thumb.jpg");
        public Uri? LogoUrl { get; } = new Uri("https://images-assets.nasa.gov/image/PIA12235/PIA12235~thumb.jpg");
        public ProjectId Id { get; } = new ProjectId("test");
        public IEnhancedCommand Refresh { get; } = EnhancedCommand.Create(() => { });
        public IAmountUI Goal { get; set; } = AmountUI.FromBtc(2m);
        public IObservable<IAmountUI> Funded { get; } = Observable.Return(AmountUI.FromBtc(0.75m));
        public IObservable<IReadOnlyCollection<IPayment>> Payments { get; set; } = Observable.Return<IReadOnlyCollection<IPayment>>([]);
        public DateTimeOffset FundingStart { get; } = DateTimeOffset.Now.AddDays(-14);
        public DateTimeOffset FundingEnd { get; init; } = DateTimeOffset.Now.AddDays(30);
        public DateTimeOffset TransactionDate { get; set; } = DateTimeOffset.Now.AddDays(-1);
        public FundingStatus Status { get; set; } = FundingStatus.Waiting;
    }
}
