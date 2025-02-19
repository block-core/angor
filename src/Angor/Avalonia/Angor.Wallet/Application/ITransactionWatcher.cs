using Angor.Wallet.Domain;

namespace Angor.Wallet.Application;

public interface ITransactionWatcher
{
    IObservable<BroadcastedTransaction> Watch(WalletId id);
}