using Angor.Sdk.Funding.Shared;
using AngorApp.UI.Sections.Shared.New;

namespace AngorApp.UI.Sections.Funded.Manage
{
    public class InvestmentProjectSample2 : IInvestmentProject2
    {
        public string Name { get; } = "Sample";
        public string Description { get; } = "Description";
        public Uri? BannerUrl { get; } = new Uri("https://images-assets.nasa.gov/image/PIA12235/PIA12235~thumb.jpg");
        public Uri? LogoUrl { get; } = new Uri("https://images-assets.nasa.gov/image/PIA12235/PIA12235~thumb.jpg");
        public ProjectId Id { get; } = new ProjectId("test");
        public IEnhancedCommand Refresh { get; }
        public IAmountUI Target { get; } = AmountUI.FromBtc(2);
        public IObservable<IAmountUI> Raised { get; } = Observable.Return(AmountUI.FromBtc(1));
        public IObservable<IEnumerable<IStage2>> Stages { get; }
    }
}