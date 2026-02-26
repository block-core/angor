using Angor.Sdk.Funding.Founder;

namespace AngorApp.UI.Sections.FundedV2.Common.Model
{
    public interface IInvestorData
    {
        public IEnhancedCommand Refresh { get; }
        string InvestmentId { get; }
        string ProjectId { get; }
        IObservable<InvestmentStatus> Status { get; }
    }
}