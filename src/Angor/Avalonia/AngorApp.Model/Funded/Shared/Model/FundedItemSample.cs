using AngorApp.Model.Funded.Investment.Model;

namespace AngorApp.Model.Funded.Shared.Model
{
    public class FundedItemSample : IFundedItem
    {
        public IFunded Funded { get; } = new InvestmentFundedSample();
        public IEnhancedCommand Manage { get; }
    }
}
