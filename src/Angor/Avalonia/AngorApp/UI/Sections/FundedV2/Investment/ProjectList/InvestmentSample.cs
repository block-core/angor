using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Investor;

namespace AngorApp.UI.Sections.FundedV2.Investment.ProjectList
{
    public class InvestmentSample : IInvestmentItem
    {
        public string InvestmentId { get; set; } = "sample-investment-id";
        public IObservable<IAmountUI> Amount { get; set; }
        public DateTimeOffset Date { get; set; }
        public IObservable<InvestmentStatus> Status { get; set; }
        public IEnhancedCommand<Result<InvestedProjectDto>> Refresh { get; } = ReactiveCommand.Create(() => Result.Failure<InvestedProjectDto>("Design-only command")).Enhance();
    }
}
