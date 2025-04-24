using System.Threading.Tasks;
using Angor.Contexts.Wallet.Application;
using Angor.Contexts.Wallet.Domain;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Helpers;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;
using Zafiro.UI;

namespace AngorApp.Sections.Wallet.Operate.Send.TransactionDraft;

public partial class TransactionDraftViewModel : ReactiveValidationObject, ITransactionDraftViewModel
{
    private readonly WalletId walletId;
    private readonly IWalletAppService walletAppService;
    [Reactive] private long feerate = 1;
    [ObservableAsProperty] private ITransactionDraft? transactionDraft;

    public TransactionDraftViewModel(WalletId walletId, IWalletAppService walletAppService, Destination destination, UIServices services)
    {
        this.walletId = walletId;
        this.walletAppService = walletAppService;
        Destination = destination;
        CreateDraft = ReactiveCommand.CreateFromTask<Result<ITransactionDraft>>(() => CreateDraftTo(destination.Amount, destination.BitcoinAddress, Feerate));
        transactionDraftHelper = CreateDraft.Successes().ToProperty(this, x => x.TransactionDraft);
        Confirm = ReactiveCommand.CreateFromTask<Result<TxId>>(() => TransactionDraft!.Submit(),
            this.WhenAnyValue<TransactionDraftViewModel, ITransactionDraft>(x => x.TransactionDraft!).Null().CombineLatest(CreateDraft.IsExecuting, (a, b) => !a && !b));
        TransactionConfirmed = Confirm.Successes().Select(_ => true).StartWith(false);
        IsBusy = CreateDraft.IsExecuting.CombineLatest(Confirm.IsExecuting, (a, b) => a | b);

        Confirm.HandleErrorsWith(services.NotificationService, "Could not confirm transaction");
        CreateDraft.HandleErrorsWith(services.NotificationService, "Could not create transaction preview");

        this.WhenAnyValue<TransactionDraftViewModel, long>(x => x.Feerate).ToSignal().InvokeCommand(CreateDraft);
    }

    private Task<Result<ITransactionDraft>> CreateDraftTo(long destinationAmount, string destinationBitcoinAddress, long feeRate)
    {
        var feeResult = walletAppService.EstimateFee(walletId, new Amount(destinationAmount), new Address(destinationBitcoinAddress), new DomainFeeRate(feeRate));
        
        return feeResult.Map(fee => (ITransactionDraft)new Angor.UI.Model.Implementation.Wallet.TransactionDraft(
            walletId: walletId,
            amount: destinationAmount,
            address: destinationBitcoinAddress,
            fee: fee,
            feeRate: feerate,
            walletAppService: walletAppService));
    }

    public ReactiveCommand<Unit, Result<TxId>> Confirm { get; }
    public IObservable<bool> IsBusy { get; }
    public ReactiveCommand<Unit, Result<ITransactionDraft>> CreateDraft { get; }
    public IObservable<bool> TransactionConfirmed { get; }
    public Destination Destination { get; }
    public IObservable<bool> IsValid => TransactionConfirmed;
    public bool AutoAdvance => true;
}