namespace Angor.Primitives;

public record Amount(long Sats)
{
    public override string ToString()
    {
        return $"{Sats} sats";
    }
}