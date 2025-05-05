namespace Angor.Contexts.Wallet.Domain;

public record BroadcastedTransaction(
    string Id,
    IEnumerable<TransactionInput> WalletInputs, 
    IEnumerable<TransactionOutput> WalletOutputs, 
    IEnumerable<TransactionInput> AllInputs, 
    IEnumerable<TransactionOutput> AllOutputs, 
    long Fee,
    bool IsConfirmed,
    long? BlockHeight,
    DateTimeOffset? BlockTime,
    string RawJson
);