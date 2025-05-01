namespace AngorApp.Sections.Wallet.Operate;

public class TransactionViewModelDesign : ITransactionViewModel
{
    public ReactiveCommand<Unit, Unit> ShowJson { get; }
    public IBroadcastedTransaction Transaction { get; set; } = new BroadcastedTransactionDesign()
    {
        Balance = new AmountUI(123),
        Id = "1231254",
        BlockTime = DateTimeOffset.Now,
    };
}