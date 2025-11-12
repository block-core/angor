namespace AngorApp.UI.Sections.Wallet.Main;

public class BroadcastedTransactionSample : IBroadcastedTransaction
{
    public string Id { get; set; }
    public string RawJson { get; init; }
    public IAmountUI Balance { get; set; }
    public DateTimeOffset? BlockTime { get; set; }
    public IEnhancedCommand ShowJson { get; set; }
}