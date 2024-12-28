using AngorApp.Model;
using CSharpFunctionalExtensions;
using Zafiro.Avalonia.Controls.Wizards.Builder;

namespace AngorApp.Common.TransactionPreview;

public interface ITransactionPreviewViewModel : IStep
{
    public IUnsignedTransaction Transaction { get; }
    ReactiveCommand<Unit, Result<IBroadcastedTransaction>> Confirm { get; }
    ReactiveCommand<Unit, Result<IUnsignedTransaction>> CreateTransaction { get; }
    public IObservable<bool> TransactionConfirmed { get; }
    public Destination Destination { get; }
    public ulong Feerate { get; set; }
}