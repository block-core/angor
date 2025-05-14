using Angor.Contexts.Wallet.Domain;

namespace Angor.UI.Model.Implementation.Wallet;

public class BroadcastedTransactionImpl(BroadcastedTransaction transaction) : IBroadcastedTransaction
{
    public IEnumerable<TransactionOutput> AllOutputs { get; } = transaction.AllOutputs;

    public IEnumerable<TransactionInput> AllInputs { get; } = transaction.AllInputs;

    public IEnumerable<TransactionInput> WalletOutputs { get; } = transaction.WalletInputs;

    public IEnumerable<TransactionInput> WalletInputs { get; } = transaction.WalletInputs;
    
    public string Id { get; } = transaction.Id;
    
    public long TotalFee { get; } = transaction.Fee;
    public IAmountUI Balance { get; } = new AmountUI(transaction.GetBalance().Sats);
    public DateTimeOffset? BlockTime { get; } = transaction.BlockTime;
    public string RawJson { get; } = transaction.RawJson;
}