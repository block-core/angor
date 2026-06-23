namespace Angor.Shared.Networks;

public static class Networks
{
    public static AngorNetworksSelector Bitcoin => new();
}

public class AngorNetworksSelector
{
    public Func<AngorNetwork> Mainnet { get; } = AngorNetwork.Main;
    public Func<AngorNetwork> Testnet { get; } = AngorNetwork.Testnet;
    public Func<AngorNetwork> Testnet4 { get; } = AngorNetwork.Testnet4;
    public Func<AngorNetwork> Regtest { get; } = AngorNetwork.Regtest;
    public Func<AngorNetwork> AngorNet { get; } = AngorNetwork.Angornet;
    public Func<AngorNetwork> SigNet { get; } = AngorNetwork.Signet;
    public Func<AngorNetwork> LiquidNet { get; } = AngorNetwork.Liquid;

    public static AngorNetwork NetworkByName(string networkName) => AngorNetwork.FromName(networkName);
}
