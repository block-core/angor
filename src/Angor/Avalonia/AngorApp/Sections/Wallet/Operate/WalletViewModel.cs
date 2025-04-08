using System.Linq;
using System.Windows.Input;
using Angor.Contexts.Wallet.Application;
using Angor.Contexts.Wallet.Domain;
using AngorApp.Sections.Wallet.Operate.Send;
using AngorApp.UI.Controls.Common.Success;
using AngorApp.UI.Controls.Common.TransactionDraft;
using AngorApp.UI.Services;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI.SourceGenerators;
using SuppaWallet.Gui.Wallet.Main;
using Zafiro.Avalonia.Controls.Wizards.Builder;
using Zafiro.Avalonia.Dialogs;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI;

namespace AngorApp.Sections.Wallet.Operate;

public partial class WalletViewModel : ReactiveObject, IWalletViewModel
{
    private readonly IWalletAppService walletAppService;
    private readonly UIServices uiServices;

    public WalletViewModel(IWallet wallet, IWalletAppService walletAppService, UIServices uiServices)
    {
        this.walletAppService = walletAppService;
        this.uiServices = uiServices;
        Wallet = wallet;

        GetReceiveAddress = ReactiveCommand.CreateFromTask(async () => new ResultViewModel<string>(await Wallet.GenerateReceiveAddress()));
        receiveAddressResultHelper = GetReceiveAddress.ToProperty(this, x => x.ReceiveAddressResult);
        SyncCommand = wallet.SyncCommand;
        SyncCommand.StartReactive.HandleErrorsWith(uiServices.NotificationService);

        var isInitialized = wallet.SyncCommand.StartReactive.Any(result => result.IsSuccess).StartWith(false);
        var isSyncing = wallet.SyncCommand.IsExecuting;

        // Auto stop the sync command if it fails with "Invalid" error
        SyncCommand.StartReactive
            .Failures()
            .Do(s =>
            {
                if (s.Contains("Invalid"))
                {
                    SyncCommand.StopReactive.Execute().Subscribe();
                }
            })
            .Subscribe();

        walletDisplayStatusHelper = isInitialized.CombineLatest(isSyncing, (initialized, syncing) => GetStatus(syncing, initialized)).ToProperty(this, x => x.WalletDisplayStatus);

        wallet.History.ToObservableChangeSet(x => x.Id)
            .Transform(transaction => new TransactionViewModel(transaction, uiServices))
            .TransformWithInlineUpdate<IdentityContainer<TransactionViewModel>, TransactionViewModel, string>(x => new IdentityContainer<TransactionViewModel>() { Content = x }, (x, e) => x.Content = e)
            .Bind(out var holders)
            .Subscribe();

        History = holders;
    }

    private static WalletDisplayStatus GetStatus(bool syncing, bool initialized)
    {
        if (!syncing)
        {
            return WalletDisplayStatus.Locked;
        }

        if (initialized)
        {
            return WalletDisplayStatus.Ready;
        }

        return WalletDisplayStatus.Loading;
    }

    public StoppableCommand<Unit, Result<BroadcastedTransaction>> SyncCommand { get; set; }
    public IEnumerable<IdentityContainer<TransactionViewModel>> History { get; }
    [ObservableAsProperty] private WalletDisplayStatus walletDisplayStatus;

    public IWallet Wallet { get; }

    public ICommand Send => ReactiveCommand.CreateFromTask(async () =>
    {
        var wizard = WizardBuilder.StartWith(() => new AddressAndAmountViewModel(Wallet))
            .Then(model => new TransactionDraftViewModel(Wallet.Id, walletAppService, new Destination("Test", model.Amount!.Value, model.Address!), uiServices))
            .Then(_ => new SuccessViewModel("Transaction sent!", "Success"))
            .FinishWith(_ => Unit.Default);

        return await uiServices.Dialog.ShowWizard(wizard, "Send");
    });

    public string Name { get; init; }
    public ReactiveCommand<Unit, ResultViewModel<string>> GetReceiveAddress { get; }

    [ObservableAsProperty] private ResultViewModel<string> receiveAddressResult;
}