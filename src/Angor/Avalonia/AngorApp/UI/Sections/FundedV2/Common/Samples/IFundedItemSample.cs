using AngorApp.UI.Sections.FundedV2.Common.Model;
using AngorApp.UI.Sections.FundedV2.Investment.Manage;
using AngorApp.UI.Sections.FundedV2.Investment.Model;

namespace AngorApp.UI.Sections.FundedV2.Common.Samples
{
    public class IFundedItemSample : IFundedItem
    {
        public IFunded Funded { get; } = new InvestmentFunded(new InvestmentProjectSample(), new InvestmentInvestorDataSample());
        public IEnhancedCommand Manage { get; }
    }
}
