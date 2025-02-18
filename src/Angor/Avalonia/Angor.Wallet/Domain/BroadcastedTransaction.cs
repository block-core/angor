namespace Angor.Wallet.Domain;

public record BroadcastedTransaction(
    Balance Balance,
    string Id,
    IEnumerable<TransactionInputInfo> WalletInputs, 
    IEnumerable<TransactionOutputInfo> WalletOutputs, 
    IEnumerable<TransactionAddressInfo> AllInputs, 
    IEnumerable<TransactionAddressInfo> AllOutputs, 
    long Fee,
    bool IsConfirmed,
    int? BlockHeight,
    DateTimeOffset? BlockTime,
    string RawJson
);