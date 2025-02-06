using Angor.UI.Model;

namespace AngorApp.Sections.Wallet.Operate;

public class BroadcastedTransactionDesign : IBroadcastedTransaction
{
    public string Address { get; init; }
    public decimal FeeRate { get; set; }
    public decimal TotalFee { get; set; }
    public uint Amount { get; init; }
    public string Path { get; init; }
    public int UtxoCount { get; init; }
    public string ViewRawJson { get; init; }
    
}