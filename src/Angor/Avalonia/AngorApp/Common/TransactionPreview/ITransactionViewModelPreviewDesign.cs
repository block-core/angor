using AngorApp.Model;
using AngorApp.Sections.Browse;
using CSharpFunctionalExtensions;

namespace AngorApp.Common.TransactionPreview;

public class TransactionPreviewViewModelDesign : ITransactionPreviewViewModel
{
    public IProject Project { get; }
    public IUnsignedTransaction Transaction { get; set; }
    public IObservable<bool> IsBusy { get; set; }
    public ReactiveCommand<Unit, Result<IBroadcastedTransaction>> Confirm { get; }
    public ReactiveCommand<Unit, Result<IUnsignedTransaction>> CreateTransaction { get; }
    public IObservable<bool> TransactionConfirmed { get; }
    public Destination Destination { get; } = new("Sample Destination", 0.0001m, "mzHrLAR3WWLE4eCpq82BDCKmLeYRyYXPtm");
    public decimal Feerate { get; set; } = 1m;
    public IObservable<bool> IsValid { get; }
    public bool AutoAdvance => false;
}