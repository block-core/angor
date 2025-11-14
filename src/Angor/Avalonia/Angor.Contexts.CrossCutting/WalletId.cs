namespace Angor.Contexts.CrossCutting;

public record WalletId(string Value)
{
    public override string ToString() => Value;
}

