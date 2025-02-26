namespace Angor.UI.Model;

public interface IBroadcastedTransaction
{
    public string Address { get; }
    public long FeeRate { get; }
    public long TotalFee { get; }
    public long Amount { get; }
    public string Path { get; }
    public int UtxoCount { get; }
    public string ViewRawJson { get; }
}