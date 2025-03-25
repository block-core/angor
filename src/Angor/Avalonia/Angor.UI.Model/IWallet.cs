using System.Collections.ObjectModel;
using System.Reactive;
using Angor.Wallet.Domain;
using CSharpFunctionalExtensions;
using Zafiro.UI;

namespace Angor.UI.Model;

public interface IWallet
{
    public ReadOnlyObservableCollection<IBroadcastedTransaction> History { get; }
    IObservable<long> Balance { get; }
    Task<Result<ITransactionDraft>> CreateDraft(long amount, string address, long feerate);
    Result IsAddressValid(string address);
    WalletId Id { get; }
    StoppableCommand<Unit, Result<BroadcastedTransaction>> SyncCommand { get; }
    IObservable<bool> HasTransactions { get; }
    IObservable<bool> HasBalance { get; }
    public Task<Result<string>> GenerateReceiveAddress();
}