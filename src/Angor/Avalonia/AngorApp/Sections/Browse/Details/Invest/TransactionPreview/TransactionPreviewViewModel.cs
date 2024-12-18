using System.Reactive.Linq;
using AngorApp.Sections.Wallet;
using AngorApp.Services;
using CSharpFunctionalExtensions;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Helpers;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;
using Zafiro.UI;

namespace AngorApp.Sections.Browse.Details.Invest.TransactionPreview;

public partial class TransactionPreviewViewModel : ReactiveValidationObject, ITransactionPreviewViewModel
{
    [ObservableAsProperty] private ITransaction? transaction;

    public TransactionPreviewViewModel(IWallet wallet, IProject project, UIServices services, decimal amount)
    {
        Project = project;
        Amount = amount;
        CreateTransaction = ReactiveCommand.CreateFromTask(() => wallet.CreateTransaction(amount, project.BitcoinAddress));
        transactionHelper = CreateTransaction.ToProperty(this, x => x.Transaction);
        Confirm = ReactiveCommand.CreateFromTask(() => Transaction!.Broadcast(), this.WhenAnyValue(x => x.Transaction).NotNull());
        TransactionConfirmed = Confirm.Successes().Select(_ => true).StartWith(false);
        IsBusy = CreateTransaction.IsExecuting.CombineLatest(Confirm.IsExecuting, (a, b) => a | b);

        Confirm.HandleErrorsWith(services.NotificationService, "Could not confirm transaction");
        
        CreateTransaction.Execute().Subscribe();
    }

    public IProject Project { get; }
    public decimal Amount { get; }
    public ReactiveCommand<Unit, Result> Confirm { get; }
    public IObservable<bool> IsBusy { get; }
    public ReactiveCommand<Unit, ITransaction> CreateTransaction { get; }
    public IObservable<bool> TransactionConfirmed { get; }
    public IObservable<bool> IsValid => TransactionConfirmed;
    public bool AutoAdvance => true;
}