using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Investor.Dtos;
using AngorApp.Model.Funded.Shared.Model;

namespace AngorApp.Model.Funded.Fund.Model
{
    public class FundInvestorDataSample : IFundInvestorData
    {
        public FundInvestorDataSample(InvestmentStatus status = InvestmentStatus.Invested)
        {
            Status = Observable.Return(status);
            Recovery = Observable.Return(new RecoveryState(true, false, false, false, true));
        }

        public IAmountUI Amount { get; } = AmountUI.FromBtc(0.5m);
        public DateTimeOffset InvestedOn { get; } = DateTimeOffset.Now;
        public IEnhancedCommand Refresh { get; } = EnhancedCommand.Create(() => { });
        public string InvestmentId { get; } = "funding-preview-id";
        public string ProjectId { get; }
        public IObservable<InvestmentStatus> Status { get; }
        public IObservable<RecoveryState> Recovery { get; }
        public IObservable<IReadOnlyList<InvestorStageItemDto>> StageItems { get; } = Observable.Return<IReadOnlyList<InvestorStageItemDto>>(new List<InvestorStageItemDto>());

        public void SetStatus(InvestmentStatus status)
        {
        }
    }
}
