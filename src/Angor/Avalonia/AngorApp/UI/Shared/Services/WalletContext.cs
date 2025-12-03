using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using Angor.Contexts.CrossCutting;
using Angor.Contexts.Wallet.Application;
using Angor.Contexts.Wallet.Domain;
using Angor.Contexts.Wallet.Infrastructure.Interfaces;
using DynamicData;
using Zafiro.CSharpFunctionalExtensions;

namespace AngorApp.UI.Shared.Services;

public sealed class WalletContext : IWalletContext, IDisposable
{
    private readonly IWalletAppService walletAppService;
    private readonly IWalletProvider walletProvider;
    private readonly IPasswordProvider passwordProvider;
    private readonly Func<BitcoinNetwork> bitcoinNetwork;
    private readonly CompositeDisposable disposable = new();
    private readonly SourceCache<IWallet, WalletId> sourceCache = new(wallet => wallet.Id);
    private readonly BehaviorSubject<Maybe<IWallet>> current = new(Maybe<IWallet>.None);

    public WalletContext(IWalletAppService walletAppService, IWalletProvider walletProvider, IPasswordProvider passwordProvider, Func<BitcoinNetwork> bitcoinNetwork)
    {
        this.walletAppService = walletAppService;
        this.walletProvider = walletProvider;
        this.passwordProvider = passwordProvider;
        this.bitcoinNetwork = bitcoinNetwork;
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

    public async Task<Result<IWallet>> GetDefaultWallet()
    {
        if (CurrentWallet.HasValue)
        {
            return Result.Success(CurrentWallet.Value);
        }
        
        var result = await walletAppService.CreateWallet("<default>", null, bitcoinNetwork());

        return await result
            .Bind(id => walletProvider.Get(id))
            .Tap(wallet => sourceCache.AddOrUpdate(wallet));
    }

    public void Dispose()
    {
        disposable.Dispose();
    }
}