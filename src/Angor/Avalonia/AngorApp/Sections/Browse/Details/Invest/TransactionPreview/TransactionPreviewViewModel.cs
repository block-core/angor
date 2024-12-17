using System.Reactive.Linq;
using AngorApp.Sections.Wallet;
using CSharpFunctionalExtensions;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Validation.Extensions;
using ReactiveUI.Validation.Helpers;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;
using Zafiro.UI;

namespace AngorApp.Sections.Browse.Details.Invest.TransactionPreview;

public partial class TransactionPreviewViewModel : ReactiveValidationObject, IValidatable, ITransactionPreviewViewModel
{
    [ObservableAsProperty] private ITransaction? transaction;

    public TransactionPreviewViewModel(IWallet wallet, Project project, decimal amount)
    {
        Amount = amount;
        CreateTransaction = ReactiveCommand.CreateFromTask(() => wallet.CreateTransaction(amount, project.Address));
        CreateTransaction.Execute().Subscribe();
        IsBusy = CreateTransaction.IsExecuting;
        transactionHelper = CreateTransaction.ToProperty(this, x => x.Transaction);
        Confirm = ReactiveCommand.CreateFromTask(() => Transaction!.Broadcast(), this.WhenAnyValue(x => x.Transaction).NotNull());
        TransactionConfirmed = Confirm.Successes().Select(x => true).StartWith(false);
    }

    public decimal Amount { get; }

    public ReactiveCommand<Unit, Result> Confirm { get; }
    public IObservable<bool> IsBusy { get; }

    public ReactiveCommand<Unit, ITransaction> CreateTransaction { get; }
    public IObservable<bool> TransactionConfirmed { get; }
    public IObservable<bool> IsValid => this.IsValid();
}