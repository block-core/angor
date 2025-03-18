namespace AngorApp.Sections.Wallet.Operate;

public interface ITransactionViewModel
{
    ReactiveCommand<Unit, Unit> ShowJson { get; }
    string Address { get; }
    long FeeRate { get; }
    long TotalFee { get; }
    long Amount { get; }
    string Path { get; }
    int UtxoCount { get; }
}