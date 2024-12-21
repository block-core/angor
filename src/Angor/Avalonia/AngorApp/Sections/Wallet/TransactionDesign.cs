using System.Threading.Tasks;
using AngorApp.Model;
using CSharpFunctionalExtensions;

namespace AngorApp.Sections.Wallet;

public class UnsignedTransactionDesign : IUnsignedTransaction
{
    public string Address { get; set; }
    
    public decimal FeeRate { get; set; }

    public decimal TotalFee { get; set; }
    public decimal Amount { get; set; }
    public string Path { get; set; }
    public int UtxoCount { get; set; }
    public string ViewRawJson { get; set; }
    public async Task<Result<IBroadcastedTransaction>> Broadcast()
    {
        await Task.Delay(2000);
        return new BroadcastedTransactionDesign();
    }
}