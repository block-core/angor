namespace Angor.Wallet.Domain;

public record WalletId(Guid Id)
{
    public static WalletId New()
    {
        return new WalletId(Guid.NewGuid());
    }
}