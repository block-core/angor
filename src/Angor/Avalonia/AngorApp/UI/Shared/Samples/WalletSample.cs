using System.Collections.ObjectModel;
using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Domain;
using Angor.Shared.Models;

namespace AngorApp.UI.Shared.Samples;

public class WalletSample : IWallet
{
    public ReadOnlyObservableCollection<IBroadcastedTransaction> History { get; } = new([
        new WalletBroadcastedTransactionSample { Balance = new AmountUI(1000), RawJson = "json" },
        new WalletBroadcastedTransactionSample { Balance = new AmountUI(3000), RawJson = "json" },
        new WalletBroadcastedTransactionSample { Balance = new AmountUI(43000), RawJson = "json" },
        new WalletBroadcastedTransactionSample { Balance = new AmountUI(30000), RawJson = "json" }
    ]);

    public IAmountUI Balance { get; set; } = new AmountUI(5_0000_0000);
    public IAmountUI UnconfirmedBalance { get; } = new AmountUI(1_0000_0000);
    public IAmountUI ReservedBalance { get; } = new AmountUI(5000_0000);

    public async Task<Result<ITransactionDraft>> CreateDraft(long amount, string address, long feerate)
    {
        await Task.Delay(1000);

        return new WalletTransactionDraftSample(new AmountUI(feerate * 100));
    }

    public Result IsAddressValid(string address)
    {
        return Result.Success();
    }

    public WalletId Id { get; }
    public DateTimeOffset CreatedOn { get; } = DateTimeOffset.Now;
    public string Name { get; set; } = "Default";
    public IEnhancedCommand Send { get; } = EnhancedCommand.Create(() => { });
    public IEnhancedCommand<Result<string>> GetReceiveAddress { get; } = EnhancedCommand.CreateWithResult(() => Result.Success(WalletSampleData.TestNetBitcoinAddress));
    public IEnhancedCommand<Result> GetTestCoins { get; } = EnhancedCommand.CreateWithResult(Result.Success);
    public bool CanGetTestCoins { get; set; } = true;
    public IEnhancedCommand<Result<IEnumerable<IBroadcastedTransaction>>> RefreshBalanceAndFetchHistory { get; } =
        EnhancedCommand.CreateWithResult(() => Result.Success<IEnumerable<IBroadcastedTransaction>>([]));
    public IEnhancedCommand<Result<AccountBalanceInfo>> RefreshBalance { get; } =
        EnhancedCommand.CreateWithResult(() => Result.Success(new AccountBalanceInfo()));
    public IObservable<bool> HasTransactions { get; } = Observable.Return(false);
    public IObservable<bool> HasBalance { get; } = Observable.Return(false);
    public NetworkKind NetworkKind { get; set; } = NetworkKind.Bitcoin;

    public async Task<Result<string>> GenerateReceiveAddress()
    {
        return Result.Success(WalletSampleData.TestNetBitcoinAddress);
    }
}

file sealed class WalletTransactionDraftSample(IAmountUI totalFee) : ITransactionDraft
{
    public IAmountUI TotalFee { get; set; } = totalFee;

    public Task<Result<TxId>> Confirm()
    {
        return Task.FromResult(Result.Success(new TxId("test")));
    }
}

file sealed class WalletBroadcastedTransactionSample : IBroadcastedTransaction
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string RawJson { get; init; } = "{}";
    public IAmountUI Balance { get; set; } = new AmountUI(0);
    public DateTimeOffset? BlockTime { get; set; }
    public IEnhancedCommand ShowJson { get; } = EnhancedCommand.Create(() => { });
}
