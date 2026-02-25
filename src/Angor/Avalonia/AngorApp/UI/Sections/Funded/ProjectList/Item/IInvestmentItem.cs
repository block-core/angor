using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Investor;

namespace AngorApp.UI.Sections.Funded.ProjectList.Item
{
    public interface IInvestmentItem
    {
        public string InvestmentId { get; }
        public IObservable<IAmountUI> Amount { get; }
        public DateTimeOffset Date { get; }
        public IObservable<InvestmentStatus> Status { get; }
        public IEnhancedCommand<Result<InvestedProjectDto>> Refresh { get; }
    }
}
