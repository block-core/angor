using System.Reactive.Disposables;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Angor.Contexts.Wallet.Application;
using Angor.Contexts.Wallet.Domain;
using AngorApp.UI.Controls;
using AngorApp.UI.Controls.Feerate;
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
    [ObservableAsProperty] private ITransactionDraft? draft;
    [ObservableAsProperty] private IAmountUI? fee;
    private readonly BehaviorSubject<bool> isCalculatingDraft = new(false);
    private readonly CompositeDisposable disposable = new();

    public TransactionDraftViewModel(WalletId walletId, IWalletAppService walletAppService, SendAmount sendAmount, UIServices uiServices)
    {
        this.walletId = walletId;
        this.walletAppService = walletAppService;
        this.uiServices = uiServices;
        
        isCalculatingDraft.DisposeWith(disposable);

        var createDraft = this.WhenAnyValue(x => x.Feerate)
            .WhereNotNull()
            .SelectLatest(feerate => CreateDraftTo(sendAmount.Amount, sendAmount.BitcoinAddress, feerate!.Value), isCalculatingDraft, scheduler: RxApp.MainThreadScheduler)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Publish();
            
        createDraft.HandleErrorsWith(uiServices.NotificationService, "Could not create transaction preview").DisposeWith(disposable);
        
        IsCalculating = isCalculatingDraft.AsObservable().ObserveOn(RxApp.MainThreadScheduler);
        
        draftHelper = createDraft
            .Successes()
            .ToProperty(this, model => model.Draft)
            .DisposeWith(disposable);

        var canConfirm = this.WhenAnyValue(model => model.Draft)
            .NotNull()
            .CombineLatest(IsCalculating, (hasDraft, calculating) => hasDraft && !calculating);
        
        Confirm = ReactiveCommand.CreateFromTask(() => Draft!.Confirm(), canConfirm).DisposeWith(disposable);
        Confirm.HandleErrorsWith(uiServices.NotificationService, "Send failed").DisposeWith(disposable);
        IsSending = Confirm.IsExecuting;
        feeHelper = this.WhenAnyValue(model => model.Draft!.TotalFee).ToProperty(this, model => model.Fee).DisposeWith(disposable);
        createDraft.Connect().DisposeWith(disposable);
    }

    public IObservable<bool> IsCalculating { get; }

    public IObservable<bool> IsSending { get; }

    public ReactiveCommand<Unit, Result<TxId>> Confirm { get; }
    
    public IEnumerable<IFeeratePreset> Presets => uiServices.FeeratePresets;

    private Task<Result<ITransactionDraft>> CreateDraftTo(long destinationAmount, string destinationBitcoinAddress, long feeRate)
    {
        var feeResult = walletAppService.EstimateFeeAndSize(walletId, new Amount(destinationAmount), new Address(destinationBitcoinAddress), new DomainFeeRate(feeRate));

        return feeResult.Map(fee => (ITransactionDraft)new Angor.UI.Model.Implementation.Wallet.TransactionDraft(
            walletId: walletId,
            amount: destinationAmount,
            address: destinationBitcoinAddress,
            feeAndSize: fee,
            feeRate: new DomainFeeRate(feeRate),
            walletAppService: walletAppService));
    }
}