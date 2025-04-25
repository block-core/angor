using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Angor.Contexts.Wallet.Domain;
using AngorApp.Sections.Browse;
using AngorApp.Sections.Wallet.Operate;
using Zafiro.UI;

namespace AngorApp.Sections.Wallet;

public class WalletDesign : IWallet
{
    private readonly Task<Result<string>> receiveAddress = Task.FromResult(Result.Success(SampleData.TestNetBitcoinAddress));

    public ReadOnlyObservableCollection<IBroadcastedTransaction> History { get; } = new([
        new BroadcastedTransactionDesign { Address = "someaddress1", Amount = 1000, UtxoCount = 12, Path = "path", ViewRawJson = "json" },
        new BroadcastedTransactionDesign { Address = "someaddress2", Amount = 3000, UtxoCount = 15, Path = "path", ViewRawJson = "json" },
        new BroadcastedTransactionDesign { Address = "someaddress3", Amount = 43000, UtxoCount = 15, Path = "path", ViewRawJson = "json" },
        new BroadcastedTransactionDesign { Address = "someaddress4", Amount = 30000, UtxoCount = 15, Path = "path", ViewRawJson = "json" }
    ]);

    public long Balance { get; } = 5_0000_0000;
    
    public async Task<Result<ITransactionDraft>> CreateDraft(long amount, string address, long feerate)
    {
        await Task.Delay(1000);

        //return Result.Failure<ITransaction>("Transaction creation failed");
        return new TransactionDraftDesign
        {
            Address = address,
            Amount = amount,
            TotalFee = new AmountUI(feerate * 100),
            FeeRate = feerate
        };
    }

    public Result IsAddressValid(string address)
    {
        return Result.Success();
    }

    public WalletId Id { get; }
    public IObservable<bool> HasTransactions { get; } = Observable.Return(false);
    public IObservable<bool> HasBalance { get; }= Observable.Return(false);
    public async Task<Result<string>> GenerateReceiveAddress()
    {
        return Result.Success("test");
    }
}