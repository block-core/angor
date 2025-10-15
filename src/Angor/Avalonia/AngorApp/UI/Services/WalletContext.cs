using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using Angor.Contexts.Wallet.Application;
using Angor.Contexts.Wallet.Domain;
using DynamicData;
using Zafiro.CSharpFunctionalExtensions;

namespace AngorApp.UI.Services;

public sealed class WalletContext : IWalletContext, IDisposable
{
    private readonly IWalletAppService walletAppService;
    private readonly CompositeDisposable disposable = new();
    private readonly SourceCache<IWallet, WalletId> sourceCache = new(wallet => wallet.Id);
    private readonly BehaviorSubject<Maybe<IWallet>> current = new(Maybe<IWallet>.None);

    public WalletContext(IWalletAppService walletAppService, IWalletProvider walletProvider)
    {
        this.walletAppService = walletAppService;
        LoadInitialWallets(walletAppService, walletProvider, sourceCache).DisposeWith(disposable);

        WalletChanges = sourceCache.Connect();
        WalletChanges.OnItemAdded(added => CurrentWallet = added.AsMaybe()).Subscribe().DisposeWith(disposable);
        WalletChanges.OnItemRemoved(removed => CurrentWallet = sourceCache.Items.TryFirst(wallet => !wallet.Equals(removed))).Subscribe().DisposeWith(disposable);
    }

    private static IDisposable LoadInitialWallets(IWalletAppService walletAppService, IWalletProvider walletProvider, SourceCache<IWallet, WalletId> sourceCache)
    {
        var existingWallets = Observable.FromAsync(() => walletAppService.GetMetadatas().Bind(metadatas => metadatas.Select(metadata => walletProvider.Get(metadata.Id)).CombineInOrder()));
        var successes = existingWallets.Successes();
        return sourceCache.PopulateFrom(successes);
    }

    public IObservable<IChangeSet<IWallet, WalletId>> WalletChanges { get; }
    public IObservable<Maybe<IWallet>> CurrentWalletChanges => current.DistinctUntilChanged();
    public Maybe<IWallet> CurrentWallet
    {
        get => current.Value;
        set => current.OnNext(value);
    }

    public async Task<Result> DeleteWallet(WalletId walletId)
    {
        var deleteResult = await walletAppService.DeleteWallet(walletId);
        if (deleteResult.IsFailure)
        {
            return deleteResult;
        }

        var existing = sourceCache.Lookup(walletId);
        if (existing.HasValue)
        {
            if (existing.Value is IDisposable disposableWallet)
            {
                disposableWallet.Dispose();
            }

            sourceCache.Remove(existing.Value);
        }

        CurrentWallet = sourceCache.Items.TryFirst();
        return Result.Success();
    }

    public void Dispose()
    {
        disposable.Dispose();
    }
}
