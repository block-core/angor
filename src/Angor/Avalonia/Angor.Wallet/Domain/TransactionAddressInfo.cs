namespace Angor.Wallet.Domain;

public record TransactionAddressInfo(
    string Address,
    ulong TotalAmount
);