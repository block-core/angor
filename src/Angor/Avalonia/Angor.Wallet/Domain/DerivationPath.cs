namespace Angor.Wallet.Domain;

public record DerivationPath
{
    public uint Purpose { get; }
    public uint CoinType { get; }
    public uint Account { get; }

    private DerivationPath(uint purpose, uint coinType, uint account)
    {
        Purpose = purpose;
        CoinType = coinType;
        Account = account;
    }

    public static DerivationPath Create(uint purpose, uint coinType, uint account) =>
        new(purpose, coinType, account);

    public override string ToString() => $"{Purpose}'{CoinType}'{Account}'";
}