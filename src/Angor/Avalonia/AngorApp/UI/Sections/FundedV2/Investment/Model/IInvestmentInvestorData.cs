using AngorApp.UI.Sections.FundedV2.Common.Model;

namespace AngorApp.UI.Sections.FundedV2.Investment.Model
{
    public interface IInvestmentInvestorData : IInvestorData
    {
        public IAmountUI Amount { get; }
        public DateTimeOffset InvestedOn { get; }
    }
}