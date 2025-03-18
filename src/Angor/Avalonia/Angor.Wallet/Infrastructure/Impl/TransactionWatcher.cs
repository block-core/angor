using System.Reactive.Linq;
using Angor.Wallet.Application;
using Angor.Wallet.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Wallet.Infrastructure.Impl
{
    public class TransactionWatcher(IWalletAppService walletAppService) : ITransactionWatcher
    {
        public IObservable<Result<BroadcastedTransaction>> Watch(WalletId id)
        {
            var initial = Observable.Defer(() => GetTransactions(id));
            
            var polling = Observable.Timer(TimeSpan.FromSeconds(10))
                .SelectMany(_ => GetTransactions(id))
                .Repeat();
            
            var source = initial.Concat(polling);

            return source.SelectMany(result => 
                    result.Match(
                        success => success.Select(Result.Success),
                        error => [Result.Failure<BroadcastedTransaction>(error)]
                    )
                )
                .Publish()
                .RefCount();
        }

        private IObservable<Result<IEnumerable<BroadcastedTransaction>>> GetTransactions(WalletId id)
        {
            return Observable.FromAsync(() => walletAppService.GetTransactions(id));
        }
    }
}