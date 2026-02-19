namespace Angor.Sdk.Common;

public record Amount(long Sats)
{
    public override string ToString()
    {
        return $"{Sats} sats";
    }
}

