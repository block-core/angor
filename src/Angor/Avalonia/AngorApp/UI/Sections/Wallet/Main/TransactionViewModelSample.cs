namespace AngorApp.UI.Sections.Wallet.Main;

public class TransactionViewModelSample : ITransactionViewModel
{
    public ReactiveCommand<Unit, Unit> ShowJson { get; }
    public IBroadcastedTransaction Transaction { get; set; } = new BroadcastedTransactionSample()
    {
        Balance = new AmountUI(123),
        Id = "1231254",
        BlockTime = DateTimeOffset.Now,
    };
}