using AngorApp.UI.Sections.Funded.Manage;

namespace AngorApp.UI.Sections.Funded.Section
{
    public class FundedSectionViewModelSample : IFundedSectionViewModel
    {
        public IEnhancedCommand FindProjects { get; }

        public IReadOnlyCollection<IFundedItem2> FundedItems { get; } =
        [
            new FundedItem2(new Funded2(new InvestmentProjectSample2(), new InvestmentInvestorData2Sample()), EnhancedCommand.Create(() =>
            {
            })),
            new FundedItem2(new Funded2(new FundProjectSample2(), new FundInvestorData2Sample()), EnhancedCommand.Create(() =>
            {
            })),
        ];

        public IEnhancedCommand Refresh { get; }
    }
}