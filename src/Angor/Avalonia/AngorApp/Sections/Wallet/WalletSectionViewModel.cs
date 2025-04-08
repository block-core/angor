using System.Linq;
using System.Threading.Tasks;
using Angor.Contexts.Wallet.Application;
using AngorApp.Sections.Wallet.CreateAndRecover;
using AngorApp.Sections.Wallet.Operate;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;
using Zafiro.CSharpFunctionalExtensions;
using WalletViewModel = AngorApp.Sections.Wallet.Operate.WalletViewModel;

namespace AngorApp.Sections.Wallet;

public partial class WalletSectionViewModel : ReactiveObject, IWalletSectionViewModel
{
    [ObservableAsProperty] private IWalletViewModel? wallet;

    public WalletSectionViewModel(UIServices uiServices, IWalletWizard walletWizard, IWalletAppService walletAppService, IWalletBuilder walletBuilder)
    {
        CreateWallet = ReactiveCommand.CreateFromTask(() => walletWizard.CreateNew());
        RecoverWallet = ReactiveCommand.CreateFromTask(() => walletWizard.Recover());

        walletHelper = uiServices.ActiveWallet.CurrentChanged
            .Merge(Observable.Return(uiServices.ActiveWallet.Current).Values())
            .Select(w => new WalletViewModel(w, walletAppService, uiServices))
            .ToProperty(this, x => x.Wallet);
        
        TryLoadExistingWallet = ReactiveCommand.CreateFromTask(() => SetDefaultWalletIfAny(walletAppService, walletBuilder, uiServices));
        TryLoadExistingWallet.Execute().Subscribe();
        IsBusy = TryLoadExistingWallet.IsExecuting;
    }

    public IObservable<bool> IsBusy { get; }

    private static async Task SetDefaultWalletIfAny(IWalletAppService walletAppService, IWalletBuilder walletBuilder, UIServices uiServices)
    {
        var result = await walletAppService.GetMetadatas().Map(tuples => tuples.ToList());
        if (result.IsFailure)
        {
            return;
        }
            
        var walletInfos = result.Value;
        if (walletInfos.Count == 0 || uiServices.ActiveWallet.Current.HasValue)
        {
            return;
        }

        var walletInfo = walletInfos.First();
        await walletBuilder.Create(walletInfo.Id)
            .Tap(w => uiServices.ActiveWallet.Current = w.AsMaybe());
    }

    public ReactiveCommand<Unit,Unit> TryLoadExistingWallet { get; }

    public ReactiveCommand<Unit, Maybe<Unit>> CreateWallet { get; }
    public ReactiveCommand<Unit, Maybe<Unit>> RecoverWallet { get; }
}