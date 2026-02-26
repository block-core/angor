using Angor.Sdk.Funding.Founder;

namespace AngorApp.UI.Sections.FundedV2.Investment.Model
{
    internal class InvestmentInvestorDataSample : IInvestmentInvestorData
    {
        public InvestmentInvestorDataSample(InvestmentStatus status = InvestmentStatus.Invested)
        {
            Status = Observable.Return(status);
        }

        public IAmountUI Amount { get; } = new AmountUI(10000000);
        public DateTimeOffset InvestedOn { get; } = DateTimeOffset.Now;
        public IEnhancedCommand Refresh { get; } = EnhancedCommand.Create(() => { });
        public string InvestmentId { get; } = "investment-preview-id";
        public string ProjectId { get; }
        public IObservable<InvestmentStatus> Status { get; }
    }
}
