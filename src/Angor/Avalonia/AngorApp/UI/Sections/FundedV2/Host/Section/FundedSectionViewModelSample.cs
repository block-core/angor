using AngorApp.UI.Sections.FundedV2.Common.Model;
using AngorApp.UI.Sections.FundedV2.Fund.Manage;
using AngorApp.UI.Sections.FundedV2.Fund.Model;
using AngorApp.UI.Sections.FundedV2.Investment.Manage;
using AngorApp.UI.Sections.FundedV2.Investment.Model;

namespace AngorApp.UI.Sections.FundedV2.Host.Section
{
    public class FundedSectionViewModelSample : IFundedSectionViewModel
    {
        public IEnhancedCommand FindProjects { get; }

        public IReadOnlyCollection<IFundedItem> FundedItems { get; } =
        [
            new FundedItem(new InvestmentFunded(new InvestmentProjectSample(), new InvestmentInvestorDataSample()), EnhancedCommand.Create(() =>
            {
            })),
            new FundedItem(new FundFunded(new FundProjectSample(), new FundInvestorDataSample()), EnhancedCommand.Create(() =>
            {
            })),
        ];

        public IEnhancedCommand Refresh { get; }
    }
}
