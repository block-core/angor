using System.Threading.Tasks;
using Angor.UI.Model;
using CSharpFunctionalExtensions;

namespace AngorApp.Sections.Wallet.Operate;

public class UnsignedTransactionDesign : IUnsignedTransaction
{
    public string Address { get; set; }
    
    public long FeeRate { get; set; }

    public long TotalFee { get; set; }
    public long Amount { get; set; }
    public string Path { get; set; }
    public int UtxoCount { get; set; }
    public string ViewRawJson { get; set; }
    
    public async Task<Result<IBroadcastedTransaction>> Broadcast()
    {
        await Task.Delay(3000);
        return new BroadcastedTransactionDesign();
    }
}