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
    public Destination Destination { get; } = new Destination("Sample Destination", 0.0001m, "mzHrLAR3WWLE4eCpq82BDCKmLeYRyYXPtm");
    public IProject Project { get; }
    public IObservable<bool> IsValid { get; }
    public bool AutoAdvance => false;
}