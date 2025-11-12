using System.Reactive.Disposables;
using AngorApp.UI.Sections.Wallet.CreateAndImport;
using DynamicData;
using Zafiro.CSharpFunctionalExtensions;

namespace AngorApp.UI.Sections.Wallet.Main;

public partial class WalletSectionViewModel : ReactiveObject, IWalletSectionViewModel, IDisposable
{
    private readonly CompositeDisposable disposable = new();

    [Reactive]
    private IWallet? currentWallet;

    public WalletSectionViewModel(IWalletContext walletContext, WalletCreationWizard creationWizard, WalletImportWizard importWizard)
    {
        walletContext
            .WalletChanges
            .Bind(out var wallets)
            .Subscribe()
            .DisposeWith(disposable);
        
        Wallets = wallets;

        Import = ReactiveCommand.CreateFromTask(importWizard.Start).Enhance().DisposeWith(disposable);
        Create = ReactiveCommand.CreateFromTask(creationWizard.Start).Enhance().DisposeWith(disposable);
        
        CurrentWallet = walletContext.CurrentWallet.GetValueOrDefault();
        walletContext.CurrentWalletChanges.Select(maybe => maybe.GetValueOrDefault()).BindTo(this, x => x.CurrentWallet).DisposeWith(disposable);
        this.WhenAnyValue(model => model.CurrentWallet).Select(wallet => wallet.AsMaybe()).BindTo(walletContext, context2 => context2.CurrentWallet)
            .DisposeWith(disposable);
    }

    public IEnhancedCommand Create { get; }
    public IEnhancedCommand Import { get; }
    public IEnumerable<IWallet> Wallets { get; }

    public void Dispose()
    {
        disposable.Dispose();
    }
}