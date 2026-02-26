using AngorApp.UI.Sections.FundedV2.Common.Model;

namespace AngorApp.UI.Sections.FundedV2.Fund.Model
{
    public interface IFundInvestorData : IInvestorData
    {
        public IAmountUI Amount { get; }
    }
}
