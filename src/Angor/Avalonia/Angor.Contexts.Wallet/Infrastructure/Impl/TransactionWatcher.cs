using System.Reactive.Linq;
using Angor.Contexts.Wallet.Application;
using Angor.Contexts.Wallet.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Contexts.Wallet.Infrastructure.Impl
{
    public class TransactionWatcher(IWalletAppService walletAppService) : ITransactionWatcher
    {
        public IObservable<Result<Event>> Watch(WalletId id)
        {
            var initial = Observable.Defer(() => GetTransactions(id));
            
            var polling = Observable.Timer(TimeSpan.FromMinutes(10)) // TODO we really should check if the block height and block hash changed
                .SelectMany(_ => GetTransactions(id))
                .Repeat();
            
            var source = initial.Concat(polling);

            return source.SelectMany(result => 
                    result.Match(
                        success => success.Select(Result.Success),
                        error => [Result.Failure<Event>(error)]
                    )
                )
                .Publish()
                .RefCount();
        }

        private IObservable<Result<IEnumerable<Event>>> GetTransactions(WalletId id)
        {
            return Observable.FromAsync(() =>
            {
                var task = walletAppService.GetTransactions(id)
                    .Map(enumerable =>
                    {
                        if (!enumerable.Any())
                        {
                            return [new WalletEmptyEvent()];
                        }
                        return enumerable.Select(Event (transaction) => new TransactionEvent(transaction));
                    });
                return task;
            });
        }
    }
}