using System.Reactive.Linq;
using AngorApp.Model;
using AngorApp.Services;
using CSharpFunctionalExtensions;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Helpers;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;
using Zafiro.UI;

namespace AngorApp.Common.TransactionPreview;

public partial class TransactionPreviewViewModel : ReactiveValidationObject, ITransactionPreviewViewModel
{
    [Reactive] private decimal feerate = 1m;
    [ObservableAsProperty] private IUnsignedTransaction? transaction;

    public TransactionPreviewViewModel(IWallet wallet, Destination destination, UIServices services)
    {
        Destination = destination;
        CreateTransaction = ReactiveCommand.CreateFromTask(() => wallet.CreateTransaction(destination.Amount, destination.BitcoinAddress, Feerate));
        transactionHelper = CreateTransaction.Successes().ToProperty(this, x => x.Transaction);
        Confirm = ReactiveCommand.CreateFromTask(() => Transaction!.Broadcast(), this.WhenAnyValue(x => x.Transaction).Null().CombineLatest(CreateTransaction.IsExecuting, (a, b) => !a && !b));
        TransactionConfirmed = Confirm.Successes().Select(_ => true).StartWith(false);
        IsBusy = CreateTransaction.IsExecuting.CombineLatest(Confirm.IsExecuting, (a, b) => a | b);

        Confirm.HandleErrorsWith(services.NotificationService, "Could not confirm transaction");

        this.WhenAnyValue(x => x.Feerate).ToSignal().InvokeCommand(CreateTransaction);
    }


    public ReactiveCommand<Unit, Result<IBroadcastedTransaction>> Confirm { get; }
    public IObservable<bool> IsBusy { get; }
    public ReactiveCommand<Unit, Result<IUnsignedTransaction>> CreateTransaction { get; }
    public IObservable<bool> TransactionConfirmed { get; }
    public Destination Destination { get; }
    public IObservable<bool> IsValid => TransactionConfirmed;
    public bool AutoAdvance => true;
}