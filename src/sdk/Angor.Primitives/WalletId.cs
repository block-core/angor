namespace Angor.Primitives;

public record WalletId(string Value)
{
    public override string ToString() => Value;
}