using System.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Angor.Contexts.Wallet.Application;
using Zafiro.CSharpFunctionalExtensions;

namespace AngorApp.UI.Services;

public class WalletRoot(IActiveWallet activeWallet, IWalletAppService walletAppService, IWalletBuilder walletBuilder) : IWalletRoot
{
    public Task<Result<Maybe<IWallet>>> GetDefaultWalletAndActivate() => GetCurrent().Tap(wallet => wallet.Execute(activeWallet.SetCurrent));
    public IObservable<bool> HasDefault()
    {
        var hasMetadatas = Observable.FromAsync(() => walletAppService.GetMetadatas().Map(x => x.Any())).Successes();
        var hasActiveWallet = activeWallet.HasWallet; 
        return Observable.CombineLatest(hasMetadatas, hasActiveWallet, (meta, active) => meta || active);
    }

    public Task<Result<Maybe<IWallet>>> GetCurrent()
    {
        return walletAppService.GetMetadatas()
            .Map(metadatas => metadatas.TryFirst())
            .Bind(maybeMetadata => maybeMetadata
                .Match(
                    metadata => walletBuilder.Get(metadata.Id)
                        .Map(Maybe<IWallet>.From),
                    () => Task.FromResult(Result.Success(Maybe<IWallet>.None))
                )
            );
    } 
}

public class ActiveWallet : IActiveWallet
{
    private Maybe<IWallet> current = Maybe<IWallet>.None; 
    
    private readonly BehaviorSubject<Maybe<IWallet>> currentWallet = new(Maybe<IWallet>.None);
    public IObservable<bool> HasWallet => CurrentChanged.Any().StartWith(false);

    public void SetCurrent(IWallet wallet)
    {
        current = wallet.AsMaybe();
        currentWallet.OnNext(current);
    }

    public Maybe<IWallet> Current
    {
        get => currentWallet.Value;
        set => currentWallet.OnNext(value);
    }

    public IObservable<IWallet> CurrentChanged => currentWallet.Values().AsObservable();
}