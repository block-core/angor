namespace Angor.Sdk.Wallet.Domain;

public record Balance(long Sats)
{
    public override string ToString()
    {
        return $"{Sats} sats";
    }
}