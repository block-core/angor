namespace AngorApp.Sections.Wallet.Main;

public class BroadcastedTransactionDesign : IBroadcastedTransaction
{
    public string Id { get; set; }
    public string RawJson { get; init; }
    public IAmountUI Balance { get; set; }
    public DateTimeOffset? BlockTime { get; set; }
}