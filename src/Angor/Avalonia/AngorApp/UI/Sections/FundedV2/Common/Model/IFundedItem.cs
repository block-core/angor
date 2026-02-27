namespace AngorApp.UI.Sections.FundedV2.Common.Model
{
    public interface IFundedItem
    {
        IFunded Funded { get; }
        IEnhancedCommand Manage { get; }
    }
}