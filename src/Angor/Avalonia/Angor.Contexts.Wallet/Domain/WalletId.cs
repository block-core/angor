namespace Angor.Contexts.Wallet.Domain;

public record WalletId(Guid Value)
{
    public static WalletId New()
    {
        return new WalletId(Guid.NewGuid());
    }
}