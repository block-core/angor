namespace AngorApp.UI.Sections.Wallet.Main;

public interface ITransactionViewModel
{
    ReactiveCommand<Unit, Unit> ShowJson { get; }
    IBroadcastedTransaction Transaction { get; }
}