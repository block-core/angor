namespace Angor.Wallet.Domain;

public record TransactionAddressInfo(
    string Address,
    long TotalAmount
);