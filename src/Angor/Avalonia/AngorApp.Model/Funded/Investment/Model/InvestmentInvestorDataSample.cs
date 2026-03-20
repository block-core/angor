using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Investor.Dtos;
using AngorApp.Model.Funded.Shared.Model;

namespace AngorApp.Model.Funded.Investment.Model
{
    public class InvestmentInvestorDataSample : IInvestmentInvestorData
    {
        public InvestmentInvestorDataSample()
        {
        }
        
        public InvestmentInvestorDataSample(InvestmentStatus status = InvestmentStatus.Invested)
        {
            Status = Observable.Return(status);
            Recovery = Observable.Return(new RecoveryState(true, false, false, false, true));
        }

        public IAmountUI Amount { get; } = new AmountUI(10000000);
        public DateTimeOffset InvestedOn { get; } = DateTimeOffset.Now;
        public IEnhancedCommand Refresh { get; } = EnhancedCommand.Create(() => { });
        public string InvestmentId { get; } = "investment-preview-id";
        public string ProjectId { get; }
        public IObservable<InvestmentStatus> Status { get; set; }
        public IObservable<RecoveryState> Recovery { get; }
        public IObservable<IReadOnlyList<InvestorStageItemDto>> StageItems { get; set; } = Observable.Return<IReadOnlyList<InvestorStageItemDto>>(new List<InvestorStageItemDto>());

        public void SetStatus(InvestmentStatus status)
        {
        }
    }
}
