using Angor.Contexts.Wallet.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Wallet.Application;

public interface ITransactionWatcher
{
    IObservable<Result<Event>> Watch(WalletId id);
}

public class Event;

public class WalletEmptyEvent : Event;

public class TransactionEvent(BroadcastedTransaction transaction) : Event
{
    public BroadcastedTransaction Transaction { get; } = transaction;
}