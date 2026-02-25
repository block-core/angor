namespace AngorApp.Model.Funded.Shared.Model
{
    public class FundedItem(IFunded funded, IEnhancedCommand manage) : IFundedItem
    {
        public IFunded Funded { get; } = funded;
        public IEnhancedCommand Manage { get; } = manage;
    }
}