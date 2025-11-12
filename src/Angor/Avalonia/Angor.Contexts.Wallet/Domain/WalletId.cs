namespace Angor.Contexts.Wallet.Domain;

public record WalletId(string Value)
{
    public static WalletId New()
    {
        return new WalletId(Guid.NewGuid().ToString());
    }
}