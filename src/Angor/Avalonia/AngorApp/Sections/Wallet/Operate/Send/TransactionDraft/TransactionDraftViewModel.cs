using System.Reactive.Subjects;
using System.Threading.Tasks;
using Angor.Contexts.Wallet.Application;
using Angor.Contexts.Wallet.Domain;
using AngorApp.UI.Controls;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Helpers;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;
using Zafiro.UI;
using Zafiro.UI.Reactive;

namespace AngorApp.Sections.Wallet.Operate.Send.TransactionDraft;

public partial class TransactionDraftViewModel : ReactiveValidationObject, ITransactionDraftViewModel
{
    private readonly WalletId walletId;
    private readonly IWalletAppService walletAppService;
    private readonly UIServices uiServices;
    [Reactive] private long? feerate;
    [ObservableAsProperty] private ITransactionDraft? transactionDraft;
    private readonly Subject<bool> isBusy = new();

    public TransactionDraftViewModel(WalletId walletId, 
        IWalletAppService walletAppService, 
        SendAmount sendAmount, 
        UIServices uiServices)
    {
        this.walletId = walletId;
        this.walletAppService = walletAppService;
        this.uiServices = uiServices;

        transactionDraftHelper = this.WhenAnyValue(x => x.Feerate)
            .WhereNotNull()
            .SelectLatest(f => CreateDraftTo(sendAmount.Amount, sendAmount.BitcoinAddress, f.Value), isBusy, TimeSpan.FromSeconds(1), RxApp.MainThreadScheduler)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Successes()
            .ToProperty(this, model => model.TransactionDraft);

        var canExecute = this.WhenAnyValue(model => model.TransactionDraft).NotNull();
        canExecute.Subscribe(b => { });
        Confirm = ReactiveCommand.CreateFromTask(() => TransactionDraft!.Submit(), canExecute);
        IsValid = Confirm.Any().StartWith(false);
    }

    private Task<Result<ITransactionDraft>> CreateDraftTo(long destinationAmount, string destinationBitcoinAddress, long feeRate)
    {
        var feeResult = walletAppService.EstimateFee(walletId, new Amount(destinationAmount), new Address(destinationBitcoinAddress), new DomainFeeRate(feeRate));

        return feeResult.Map(fee => (ITransactionDraft)new Angor.UI.Model.Implementation.Wallet.TransactionDraft(
            walletId: walletId,
            amount: destinationAmount,
            address: destinationBitcoinAddress,
            fee: fee,
            feeRate: new DomainFeeRate(feeRate),
            walletAppService: walletAppService));
    }

    public ReactiveCommand<Unit, Result<TxId>> Confirm { get; }

    public IObservable<bool> IsBusy { get; } = Observable.Return(false);
    public IEnumerable<IFeeratePreset> Presets => uiServices.FeeratePresets;
    public bool AutoAdvance => true;
    public IObservable<bool> IsValid { get; }
}