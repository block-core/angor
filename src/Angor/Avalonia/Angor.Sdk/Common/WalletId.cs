namespace Angor.Sdk.Common;

public record WalletId(string Value)
{
    public override string ToString() => Value;
}

