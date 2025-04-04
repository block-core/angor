namespace Angor.Contexts.Wallet.Domain;

public record TransactionAddressInfo(
    string Address,
    long TotalAmount
);