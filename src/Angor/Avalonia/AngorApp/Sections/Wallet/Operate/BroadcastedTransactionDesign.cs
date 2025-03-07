using Angor.UI.Model;

namespace AngorApp.Sections.Wallet.Operate;

public class BroadcastedTransactionDesign : IBroadcastedTransaction
{
    public string Id { get; }
    public string Address { get; init; }
    public long FeeRate { get; set; }
    public long TotalFee { get; set; }
    public long Amount { get; init; }
    public string Path { get; init; }
    public int UtxoCount { get; init; }
    public string ViewRawJson { get; init; }
    
}