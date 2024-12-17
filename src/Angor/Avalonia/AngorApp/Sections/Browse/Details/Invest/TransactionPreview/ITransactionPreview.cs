using AngorApp.Sections.Wallet;
using CSharpFunctionalExtensions;
using Zafiro.Avalonia.Controls.Wizards.Builder;

namespace AngorApp.Sections.Browse.Details.Invest.TransactionPreview;

public interface ITransactionPreviewViewModel : IStep
{
    public ITransaction Transaction { get; }
    public IObservable<bool> IsBusy { get; }
    ReactiveCommand<Unit, Result> Confirm { get; }
    ReactiveCommand<Unit, ITransaction> CreateTransaction { get; }
    public IObservable<bool> TransactionConfirmed { get; }
    IProject Project { get; }
}