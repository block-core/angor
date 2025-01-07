using System.Threading.Tasks;
using AngorApp.Model;
using CSharpFunctionalExtensions;

namespace AngorApp.Sections.Wallet.Operate;

public class UnsignedTransactionDesign : IUnsignedTransaction
{
    public string Address { get; set; }
    
    public ulong FeeRate { get; set; }

    public ulong TotalFee { get; set; }
    public ulong Amount { get; set; }
    public string Path { get; set; }
    public int UtxoCount { get; set; }
    public string ViewRawJson { get; set; }
    
    public async Task<Result<IBroadcastedTransaction>> Broadcast()
    {
        await Task.Delay(3000);
        return new BroadcastedTransactionDesign();
    }
}