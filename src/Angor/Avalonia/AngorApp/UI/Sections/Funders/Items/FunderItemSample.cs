namespace AngorApp.UI.Sections.Funders.Items;

public class FunderItemSample : IFunderItem
{
    public string Name { get; } = "Hope with Bitcoin";
    public IAmountUI Amount { get; set; } = new AmountUI(10000);
    public DateTimeOffset DateCreated { get; } = DateTimeOffset.Now;
    public IEnhancedCommand<Result> Approve { get; } = EnhancedCommand.CreateWithResult(Result.Success);
    public IEnhancedCommand<Result> Reject { get; } = EnhancedCommand.CreateWithResult(Result.Success);
    public IEnhancedCommand OpenChat { get; } = EnhancedCommand.Create(() => { });
    public FunderStatus Status { get; set; } = FunderStatus.Pending;
    public string InvestorNpub { get; } = "investor_npub";
}