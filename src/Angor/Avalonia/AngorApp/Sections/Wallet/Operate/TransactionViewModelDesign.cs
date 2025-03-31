namespace AngorApp.Sections.Wallet.Operate;

public class TransactionViewModelDesign : ITransactionViewModel
{
    public ReactiveCommand<Unit, Unit> ShowJson { get; }
    public string Address { get; init; }
    public long FeeRate { get; }
    public long TotalFee { get; }
    public long Amount { get; init; }
    public string Path { get; init; }
    public int UtxoCount { get; init; }
    public string ViewRawJson { get; set; }
}