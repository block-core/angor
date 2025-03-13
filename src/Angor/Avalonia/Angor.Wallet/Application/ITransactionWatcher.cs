using Angor.Wallet.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Wallet.Application;

public interface ITransactionWatcher
{
    IObservable<Result<BroadcastedTransaction>> Watch(WalletId id);
}