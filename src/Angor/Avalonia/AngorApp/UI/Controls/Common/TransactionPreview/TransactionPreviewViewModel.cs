using Angor.Wallet.Domain;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Helpers;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;
using Zafiro.UI;

namespace AngorApp.UI.Controls.Common.TransactionPreview;

public partial class TransactionPreviewViewModel : ReactiveValidationObject, ITransactionPreviewViewModel
{
    [Reactive] private long feerate = 1;
    [ObservableAsProperty] private ITransactionPreview? transactionPreview;

    public TransactionPreviewViewModel(IWallet wallet, Destination destination, Services.UIServices services)
    {
        Destination = destination;
        CreatePreview = ReactiveCommand.CreateFromTask(() => wallet.CreateTransaction(destination.Amount, destination.BitcoinAddress, Feerate));
        transactionPreviewHelper = CreatePreview.Successes().ToProperty(this, x => x.TransactionPreview);
        Confirm = ReactiveCommand.CreateFromTask(() => TransactionPreview!.Accept(),
            this.WhenAnyValue<TransactionPreviewViewModel, ITransactionPreview>(x => x.TransactionPreview!).Null().CombineLatest(CreatePreview.IsExecuting, (a, b) => !a && !b));
        TransactionConfirmed = Confirm.Successes().Select(_ => true).StartWith(false);
        IsBusy = CreatePreview.IsExecuting.CombineLatest(Confirm.IsExecuting, (a, b) => a | b);

        Confirm.HandleErrorsWith(services.NotificationService, "Could not confirm transaction");
        CreatePreview.HandleErrorsWith(services.NotificationService, "Could not create transaction preview");

        this.WhenAnyValue(x => x.Feerate).ToSignal().InvokeCommand(CreatePreview);
    }

    public ReactiveCommand<Unit, Result<TxId>> Confirm { get; }
    public IObservable<bool> IsBusy { get; }
    public ReactiveCommand<Unit, Result<ITransactionPreview>> CreatePreview { get; }
    public IObservable<bool> TransactionConfirmed { get; }
    public Destination Destination { get; }
    public IObservable<bool> IsValid => TransactionConfirmed;
    public bool AutoAdvance => true;
}