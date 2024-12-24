using System.Threading.Tasks;
using AngorApp.Model;
using CSharpFunctionalExtensions;

namespace AngorApp.Sections.Wallet;

public class BroadcastedTransactionDesign : IBroadcastedTransaction
{
    public string Address { get; init; }
    public decimal FeeRate { get; set; }
    public decimal TotalFee { get; set; }
    public decimal Amount { get; init; }
    public string Path { get; init; }
    public int UtxoCount { get; init; }
    public string ViewRawJson { get; init; }
    
}