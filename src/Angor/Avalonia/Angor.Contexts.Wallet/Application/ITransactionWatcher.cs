using Angor.Contexts.Wallet.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Wallet.Application;

public interface ITransactionWatcher
{
    IObservable<Result<BroadcastedTransaction>> Watch(WalletId id);
}