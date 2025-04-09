using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Angor.Contests.CrossCutting;
using Angor.Contexts.Wallet.Application;
using Angor.UI.Model;
using CSharpFunctionalExtensions;
using Zafiro.CSharpFunctionalExtensions;

namespace AngorApp.UI.Services;

public class ActiveWallet(IWalletAppService walletAppService, IWalletBuilder walletBuilder) : IActiveWallet
{
    private Maybe<IWallet> current = Maybe<IWallet>.None; 
    
    private readonly BehaviorSubject<Maybe<IWallet>> currentWallet = new(Maybe<IWallet>.None);
    public IObservable<bool> HasWallet => currentWallet.Any();
    
    public Task<Result<Maybe<IWallet>>> TryGetCurrent()
    {
        if (current.HasValue)
        {
            return Task.FromResult(Result.Success(current.Value.AsMaybe()));
        }
        
        return walletAppService.GetMetadatas()
            .Map(metadatas => metadatas.TryFirst())
            .Bind(maybeMetadata => maybeMetadata
                .Match(
                    metadata => walletBuilder.Get(metadata.Id)
                        .Map(Maybe<IWallet>.From),
                    () => Task.FromResult(Result.Success(Maybe<IWallet>.None))
                )
            ).Tap(maybe => current = maybe);
    }

    public Maybe<IWallet> Current
    {
        get => currentWallet.Value;
        set => currentWallet.OnNext(value);
    }

    public IObservable<IWallet> CurrentChanged => currentWallet.Values().AsObservable();
}