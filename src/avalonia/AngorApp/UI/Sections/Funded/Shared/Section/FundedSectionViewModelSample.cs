using AngorApp.Model.Funded.Fund.Model;
using AngorApp.Model.Funded.Investment.Model;
using AngorApp.Model.Funded.Shared.Model;

namespace AngorApp.UI.Sections.Funded.Shared.Section
{
    public class FundedSectionViewModelSample : IFundedSectionViewModel
    {
        public IEnhancedCommand FindProjects { get; }

        public IReadOnlyCollection<IFundedItem> FundedItems { get; } =
        [
            new FundedItem(new InvestmentFundedSample(), EnhancedCommand.Create(() =>
            {
            })),
            new FundedItem(new FundFundedSample(), EnhancedCommand.Create(() =>
            {
            })),
        ];

        public IEnhancedCommand Refresh { get; }
    }
}
