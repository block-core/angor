using System.Security.Cryptography;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using Blockcore.Networks;

namespace Angor.Shared.Services.Indexer.Electrum;

public static class ElectrumScriptHashUtility
{
    public static string AddressToScriptHash(string address, Network network)
    {
        var bitcoinAddress = BitcoinAddress.Create(address, network);
        var scriptPubKey = bitcoinAddress.ScriptPubKey;
        return ScriptToScriptHash(scriptPubKey);
    }

    public static string ScriptToScriptHash(Script script)
    {
        return ScriptToScriptHash(script.ToBytes());
    }

    public static string ScriptToScriptHash(byte[] scriptBytes)
    {
        var hash = SHA256.HashData(scriptBytes);
        // Electrum uses reversed byte order (little-endian)
        Array.Reverse(hash);
        return Encoders.Hex.EncodeData(hash);
    }

    public static string ScriptHexToScriptHash(string scriptHex)
    {
        var scriptBytes = Encoders.Hex.DecodeData(scriptHex);
        return ScriptToScriptHash(scriptBytes);
    }
}

public static class ElectrumExtensions
{
    public static string ToElectrumScriptHash(this BitcoinAddress address)
    {
        return ElectrumScriptHashUtility.ScriptToScriptHash(address.ScriptPubKey);
    }

    public static string ToElectrumScriptHash(this Script script)
    {
        return ElectrumScriptHashUtility.ScriptToScriptHash(script);
    }
}
