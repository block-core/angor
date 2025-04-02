using Angor.Contexts.Wallet.Domain;

namespace Angor.UI.Model.Implementation.Wallet;

public class BroadcastedTransactionImpl(BroadcastedTransaction transaction) : IBroadcastedTransaction
{
    public IEnumerable<TransactionAddressInfo> AllOutputs { get; } = transaction.AllOutputs;

    public IEnumerable<TransactionAddressInfo> AllInputs { get; } = transaction.AllInputs;

    public IEnumerable<TransactionInputInfo> WalletOutputs { get; } = transaction.WalletInputs;

    public IEnumerable<TransactionInputInfo> WalletInputs { get; } = transaction.WalletInputs;


    public string Id { get; } = transaction.Id;

    public string Address { get; } = transaction.AllOutputs.FirstOrDefault()?.Address ?? string.Empty;
    public long FeeRate { get; }
    public long TotalFee { get; } = transaction.Fee;
    public long Amount { get; } = transaction.Balance.Value;
    public string Path { get; } = "";
    public int UtxoCount => AllOutputs.Count() + AllInputs.Count();
    public string ViewRawJson { get; } = transaction.RawJson;
}