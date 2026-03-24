namespace AngorApp.Model.Funded.Shared.Model
{
    public interface IFundedItem
    {
        IFunded Funded { get; }
        IEnhancedCommand Manage { get; }
    }
}