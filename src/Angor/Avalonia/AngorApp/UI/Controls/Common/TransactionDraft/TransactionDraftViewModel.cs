using Angor.Wallet.Domain;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Helpers;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;
using Zafiro.UI;

namespace AngorApp.UI.Controls.Common.TransactionDraft;

public partial class TransactionDraftViewModel : ReactiveValidationObject, ITransactionDraftViewModel
{
    [Reactive] private long feerate = 1;
    [ObservableAsProperty] private ITransactionDraft? transactionDraft;

    public TransactionDraftViewModel(IWallet wallet, Destination destination, Services.UIServices services)
    {
        Destination = destination;
        CreateDraft = ReactiveCommand.CreateFromTask(() => wallet.CreateDraft(destination.Amount, destination.BitcoinAddress, Feerate));
        transactionDraftHelper = CreateDraft.Successes().ToProperty(this, x => x.TransactionDraft);
        Confirm = ReactiveCommand.CreateFromTask(() => TransactionDraft!.Submit(),
            this.WhenAnyValue<TransactionDraftViewModel, ITransactionDraft>(x => x.TransactionDraft!).Null().CombineLatest(CreateDraft.IsExecuting, (a, b) => !a && !b));
        TransactionConfirmed = Confirm.Successes().Select(_ => true).StartWith(false);
        IsBusy = CreateDraft.IsExecuting.CombineLatest(Confirm.IsExecuting, (a, b) => a | b);

        Confirm.HandleErrorsWith(services.NotificationService, "Could not confirm transaction");
        CreateDraft.HandleErrorsWith(services.NotificationService, "Could not create transaction preview");

        this.WhenAnyValue(x => x.Feerate).ToSignal().InvokeCommand(CreateDraft);
    }

    public ReactiveCommand<Unit, Result<TxId>> Confirm { get; }
    public IObservable<bool> IsBusy { get; }
    public ReactiveCommand<Unit, Result<ITransactionDraft>> CreateDraft { get; }
    public IObservable<bool> TransactionConfirmed { get; }
    public Destination Destination { get; }
    public IObservable<bool> IsValid => TransactionConfirmed;
    public bool AutoAdvance => true;
}