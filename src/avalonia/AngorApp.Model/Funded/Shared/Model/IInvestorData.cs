using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Investor.Dtos;

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
        IObservable<IReadOnlyList<InvestorStageItemDto>> StageItems { get; }
        public IAmountUI Amount { get; }
        void SetStatus(InvestmentStatus status);
    }
}