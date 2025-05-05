using Angor.UI.Model;

namespace AngorApp.Sections.Wallet.Operate;

public class BroadcastedTransactionDesign : IBroadcastedTransaction
{
    public string Id { get; set; }
    public string RawJson { get; init; }
    public IAmountUI Balance { get; set; }
    public DateTimeOffset? BlockTime { get; set; }
}