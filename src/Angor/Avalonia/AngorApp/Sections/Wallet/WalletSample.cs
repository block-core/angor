using System.Collections.ObjectModel;
using Angor.Contexts.Wallet.Domain;
using AngorApp.Sections.Browse;
using AngorApp.Sections.Wallet.Main;
using AngorApp.Sections.Wallet.Main;

namespace AngorApp.Sections.Wallet;

public class WalletSample : IWallet
{
    private readonly Task<Result<string>> receiveAddress = Task.FromResult(Result.Success(SampleData.TestNetBitcoinAddress));

    public ReadOnlyObservableCollection<IBroadcastedTransaction> History { get; } = new([
        new BroadcastedTransactionSample { Balance = new AmountUI(1000), RawJson = "json" },
        new BroadcastedTransactionSample { Balance = new AmountUI(3000), RawJson = "json" },
        new BroadcastedTransactionSample { Balance = new AmountUI(43000), RawJson = "json" },
        new BroadcastedTransactionSample { Balance = new AmountUI(30000), RawJson = "json" }
    ]);

    public IAmountUI Balance { get; } = new AmountUI(5_0000_0000);

    public async Task<Result<ITransactionDraft>> CreateDraft(long amount, string address, long feerate)
    {
        await Task.Delay(1000);

        return new TransactionDraftSample
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
    public IEnhancedCommand Send { get; }
    public IEnhancedCommand<Result<string>> GetReceiveAddress { get; }
    public IEnhancedCommand GetTestCoins { get; }
    public IObservable<bool> HasTransactions { get; } = Observable.Return(false);
    public IObservable<bool> HasBalance { get; } = Observable.Return(false);

    public async Task<Result<string>> GenerateReceiveAddress()
    {
        return Result.Success("test");
    }
}