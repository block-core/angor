namespace AngorApp.Sections.Wallet.Operate;

public interface ITransactionViewModel
{
    ReactiveCommand<Unit, Unit> ShowJson { get; }
    IBroadcastedTransaction Transaction { get; }
}