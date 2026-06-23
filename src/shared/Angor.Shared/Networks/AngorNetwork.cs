using NBitcoin;

namespace Angor.Shared.Networks;

/// <summary>
/// Represents an Angor-specific network configuration that wraps an NBitcoin.Network
/// with additional metadata needed for Angor operations.
/// </summary>
public class AngorNetwork
{
    /// <summary>The underlying NBitcoin network for crypto operations.</summary>
    public Network BitcoinNetwork { get; }

    /// <summary>Angor-specific network name (e.g., "Main", "Signet", "Angornet", "Liquid").</summary>
    public string Name { get; }

    /// <summary>BIP44 coin type (0 for mainnet, 1 for testnet).</summary>
    public int CoinType { get; }

    /// <summary>Whether this is a mainnet network.</summary>
    public bool IsMainnet { get; }

    /// <summary>Ticker symbol for display (e.g., "BTC" for mainnet, "tBTC" for testnet/signet).</summary>
    public string CoinTicker => IsMainnet ? "BTC" : "tBTC";

    private AngorNetwork(Network bitcoinNetwork, string name, int coinType, bool isMainnet)
    {
        BitcoinNetwork = bitcoinNetwork;
        Name = name;
        CoinType = coinType;
        IsMainnet = isMainnet;
    }

    /// <summary>
    /// Implicit conversion to NBitcoin.Network, allowing AngorNetwork to be used
    /// wherever an NBitcoin.Network is expected (e.g., Transaction.Parse, BitcoinAddress.Create).
    /// </summary>
    public static implicit operator Network(AngorNetwork angorNetwork) => angorNetwork.BitcoinNetwork;

    /// <summary>Create an empty transaction for this network.</summary>
    public Transaction CreateTransaction() => BitcoinNetwork.CreateTransaction();

    /// <summary>Parse a transaction hex string for this network.</summary>
    public Transaction CreateTransaction(string hex) => Transaction.Parse(hex, BitcoinNetwork);

    // --- Factory methods for all supported networks ---

    public static AngorNetwork Main() => new(Network.Main, "Main", 0, true);

    public static AngorNetwork Testnet() => new(Network.TestNet, "Testnet", 1, false);

    public static AngorNetwork Signet() => new(Network.TestNet, "Signet", 1, false);

    public static AngorNetwork Angornet() => new(Network.TestNet, "Angornet", 1, false);

    public static AngorNetwork Testnet4() => new(Network.TestNet, "Testnet4", 1, false);

    public static AngorNetwork Liquid() => new(NBitcoin.Altcoins.Liquid.Instance.Mainnet, "Liquid", 0, true);

    public static AngorNetwork Regtest() => new(Network.RegTest, "Regtest", 1, false);

    /// <summary>Resolve a network by name.</summary>
    public static AngorNetwork FromName(string name) => name switch
    {
        "Main" => Main(),
        "Signet" => Signet(),
        "Angornet" => Angornet(),
        "Testnet" => Testnet(),
        "Testnet4" => Testnet4(),
        "Liquid" => Liquid(),
        "Regtest" => Regtest(),
        _ => Main() // Default fallback
    };
}
