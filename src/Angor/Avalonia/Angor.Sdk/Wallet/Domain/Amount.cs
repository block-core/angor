namespace Angor.Sdk.Wallet.Domain;

public record Amount(long Sats)
{
    public override string ToString()
    {
        return $"{Sats} sats";
    }
}