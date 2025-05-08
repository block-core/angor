using System.Windows.Input;
using Angor.Contexts.Wallet.Application;
using Angor.Contexts.Wallet.Domain;
using AngorApp.UI.Controls.Common.Success;
using AngorApp.UI.Services;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI.SourceGenerators;
using Zafiro.Avalonia.Controls.Wizards.Builder;
using Zafiro.Avalonia.Dialogs;
using Zafiro.UI;
using AddressAndAmountViewModel = AngorApp.Sections.Wallet.Operate.Send.AddressAndAmount.AddressAndAmountViewModel;
using TransactionDraftViewModel = AngorApp.Sections.Wallet.Operate.Send.TransactionDraft.TransactionDraftViewModel;

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
        
        wallet.History.ToObservableChangeSet(x => x.Id)
            .Transform(transaction => new TransactionViewModel(transaction, uiServices))
            .TransformWithInlineUpdate<IdentityContainer<ITransactionViewModel>, TransactionViewModel, string>(x => new IdentityContainer<ITransactionViewModel> { Content = x }, (x, e) => x.Content = e)
            .SortAndBind(out var idContainers, SortExpressionComparer<IdentityContainer<ITransactionViewModel>>.Descending(x => x.Content.Transaction.BlockTime ?? DateTimeOffset.MinValue))
            .Subscribe();

        History = idContainers;
    }

    public IEnumerable<IdentityContainer<ITransactionViewModel>> History { get; }

    public IWallet Wallet { get; }

    public ICommand Send => ReactiveCommand.CreateFromTask(async () =>
    {
        var wizard = WizardBuilder.StartWith(() => new AddressAndAmountViewModel(Wallet))
            .Then(model => new TransactionDraftViewModel(Wallet.Id, walletAppService, new SendAmount("Test", model.Amount!.Value, model.Address!), uiServices))
            .Then(_ => new SuccessViewModel("Transaction sent!", "Success"))
            .FinishWith(_ => Unit.Default);

        return await uiServices.Dialog.ShowWizard(wizard, "Send");
    });

    public ReactiveCommand<Unit, ResultViewModel<string>> GetReceiveAddress { get; }

    [ObservableAsProperty] private ResultViewModel<string> receiveAddressResult;
}