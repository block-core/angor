using System.Reactive.Disposables;
using AngorApp.UI.Sections.Wallet.CreateAndImport;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI.Shell.Utils;

namespace AngorApp.UI.Sections.Wallet.Main;

[Section("Funds", icon: "fa-regular fa-credit-card", sortIndex: 1)]
public partial class WalletSectionViewModel : ReactiveObject, IWalletSectionViewModel, IDisposable
{
    private readonly CompositeDisposable disposable = new();

    [Reactive]
    private IWallet? currentWallet;

    public WalletSectionViewModel(IWalletContext walletContext, WalletCreationWizard creationWizard)
    {
        Wallets = walletContext.Wallets;

        Create = ReactiveCommand.CreateFromTask(creationWizard.Start).Enhance().DisposeWith(disposable);
        
        CurrentWallet = walletContext.CurrentWallet.GetValueOrDefault();
        walletContext.CurrentWalletChanges.Select(maybe => maybe.GetValueOrDefault()).BindTo(this, x => x.CurrentWallet).DisposeWith(disposable);
        this.WhenAnyValue(model => model.CurrentWallet).Select(wallet => wallet.AsMaybe()).BindTo(walletContext, context2 => context2.CurrentWallet)
            .DisposeWith(disposable);
    }

    public IEnhancedCommand Create { get; }
    public IEnumerable<IWallet> Wallets { get; }

    public void Dispose()
    {
        disposable.Dispose();
    }
}