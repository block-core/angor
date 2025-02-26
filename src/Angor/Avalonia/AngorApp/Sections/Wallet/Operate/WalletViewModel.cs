using System.Collections.ObjectModel;
using System.Windows.Input;
using Angor.Wallet.Domain;
using AngorApp.Sections.Wallet.Operate.Send;
using AngorApp.UI.Controls.Common.Success;
using AngorApp.UI.Controls.Common.TransactionPreview;
using AngorApp.UI.Services;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI.SourceGenerators;
using Zafiro.Avalonia.Controls.Wizards.Builder;
using Zafiro.Avalonia.Dialogs;
using Zafiro.UI;

namespace AngorApp.Sections.Wallet.Operate;

public partial class WalletViewModel : ReactiveObject, IWalletViewModel
{
    private readonly UIServices uiServices;

    public WalletViewModel(IWallet wallet, UIServices uiServices)
    {
        Wallet = wallet;
        this.uiServices = uiServices;

        GetReceiveAddress = ReactiveCommand.CreateFromTask(async () => new ResultViewModel<string>(await Wallet.GenerateReceiveAddress()));
        receiveAddressResultHelper = GetReceiveAddress.ToProperty(this, x => x.ReceiveAddressResult);
        SyncCommand = wallet.SyncCommand;
        var hasSomethingObs = wallet.SyncCommand.StartReactive.Any().StartWith(false)
            .CombineLatest(SyncCommand.IsExecuting, (executed, isSyncing) =>
            {
                var has = executed;
                return has;
            });
        hasSomethingHelper = hasSomethingObs.ToProperty(this, x => x.HasSomething);
        IsLoading = wallet.SyncCommand.StartReactive.Any().StartWith(false)
            .CombineLatest(SyncCommand.IsExecuting, (executed, isSyncing) =>
            {
                var isLoading = !executed && isSyncing;
                return isLoading;
            });

        Wallet.History.ToObservableChangeSet()
            .Transform(ITransactionViewModel (tx) => new TransactionViewModel(tx, this.uiServices))
            .Bind(out var history)
            .Subscribe();

        History = history;
    }

    public ReadOnlyObservableCollection<ITransactionViewModel> History { get; }

    public StoppableCommand<Unit, Result<BroadcastedTransaction>> SyncCommand { get; }
    public IObservable<bool> IsLoading { get; }
    [ObservableAsProperty] private bool hasSomething;
    public IWallet Wallet { get; }

    public ICommand Send => ReactiveCommand.CreateFromTask(async () =>
    {
        var wizard = WizardBuilder.StartWith(() => new AddressAndAmountViewModel(Wallet))
            .Then(model => new TransactionPreviewViewModel(Wallet, new Destination("Test", model.Amount!.Value, model.Address!), uiServices))
            .Then(_ => new SuccessViewModel("Transaction sent!", "Success"))
            .FinishWith(_ => Unit.Default);

        return await uiServices.Dialog.ShowWizard(wizard, "Send");
    });

    public string Name { get; set; }
    public ReactiveCommand<Unit, ResultViewModel<string>> GetReceiveAddress { get; }

    [ObservableAsProperty] private ResultViewModel<string> receiveAddressResult;
}