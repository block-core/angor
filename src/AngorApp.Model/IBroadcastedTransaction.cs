using CSharpFunctionalExtensions;

namespace AngorApp.Model;

public interface IBroadcastedTransaction
{
    public string Address { get; }
    public decimal FeeRate { get; set; }
    public decimal TotalFee { get; set; }
    public decimal Amount { get; }
    public string Path { get; }
    public int UtxoCount { get; }
    public string ViewRawJson { get; }
}