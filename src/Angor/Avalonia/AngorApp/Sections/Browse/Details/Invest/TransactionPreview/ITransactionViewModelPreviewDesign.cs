using AngorApp.Sections.Wallet;
using CSharpFunctionalExtensions;

namespace AngorApp.Sections.Browse.Details.Invest.TransactionPreview;

public class TransactionPreviewViewModelDesign : ITransactionPreviewViewModel
{
    public ITransaction Transaction { get; set; }
    public IObservable<bool> IsBusy { get; set; }
    public ReactiveCommand<Unit, Result> Confirm { get; }
    public ReactiveCommand<Unit, ITransaction> CreateTransaction { get; }
    public IObservable<bool> TransactionConfirmed { get; }
    public IObservable<bool> IsValid { get; }
    public bool AutoAdvance => false;
}