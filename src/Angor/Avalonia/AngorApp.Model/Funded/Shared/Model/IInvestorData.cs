using Angor.Sdk.Funding.Founder;

namespace AngorApp.Model.Funded.Shared.Model
{
    public interface IInvestorData
    {
        public IEnhancedCommand Refresh { get; }
        string InvestmentId { get; }
        string ProjectId { get; }
        DateTimeOffset InvestedOn { get; }
        IObservable<InvestmentStatus> Status { get; }
        IObservable<RecoveryState> Recovery { get; }
        public IAmountUI Amount { get; }
    }
}