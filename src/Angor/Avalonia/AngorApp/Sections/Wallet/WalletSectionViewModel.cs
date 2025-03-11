using System.Linq;
using System.Threading.Tasks;
using Angor.Wallet.Application;
using AngorApp.Sections.Wallet.CreateAndRecover;
using AngorApp.Sections.Wallet.Operate;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;
using SuppaWallet.Gui.Wallet.Main;
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
            .Select(w => new WalletViewModel(w, uiServices))
            .ToProperty(this, x => x.Wallet);
        
        SetDefaultWallet = ReactiveCommand.CreateFromTask(() => DoSetDefaultWallet(walletAppService, walletBuilder, uiServices));
        SetDefaultWallet.Execute().Subscribe();
    }
    
    private static async Task DoSetDefaultWallet(IWalletAppService walletAppService, IWalletBuilder walletBuilder, UIServices uiServices)
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

    public ReactiveCommand<Unit,Unit> SetDefaultWallet { get; }

    public ReactiveCommand<Unit, Maybe<Unit>> CreateWallet { get; }
    public ReactiveCommand<Unit, Maybe<Unit>> RecoverWallet { get; }
}