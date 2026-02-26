using Angor.Sdk.Funding.Founder;

namespace AngorApp.UI.Sections.FundedV2.Fund.Model
{
    public class FundInvestorDataSample : IFundInvestorData
    {
        public FundInvestorDataSample(InvestmentStatus status = InvestmentStatus.Invested)
        {
            Status = Observable.Return(status);
        }

        public IAmountUI Amount { get; } = AmountUI.FromBtc(0.5m);
        public IEnhancedCommand Refresh { get; } = EnhancedCommand.Create(() => { });
        public string InvestmentId { get; } = "funding-preview-id";
        public string ProjectId { get; }
        public IObservable<InvestmentStatus> Status { get; }
    }
}
