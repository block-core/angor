using System.Security.Cryptography;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using BlockcoreKey = Blockcore.NBitcoin.Key;

namespace Angor.Shared;

/// <summary>
/// Strongly-typed private key that inherits from NBitcoin.Key.
/// Replaces raw hex-string passing across protocol signing APIs,
/// preventing private key material from lingering as interned strings
/// on the managed heap. Dispose when done to zero backing memory.
/// </summary>
public class AngorKey : NBitcoin.Key
{
    public AngorKey() : base() { }

    public AngorKey(byte[] data, int count = -1, bool fCompressedIn = true)
        : base(data, count, fCompressedIn) { }

    /// <summary>
    /// Creates an AngorKey from a hex-encoded private key string.
    /// The intermediate byte array is zeroed immediately after construction.
    /// </summary>
    public static AngorKey FromHex(string hex)
    {
        var bytes = Encoders.Hex.DecodeData(hex);
        try
        {
            return new AngorKey(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    /// <summary>
    /// Bridge factory: converts a Blockcore.NBitcoin.Key to AngorKey.
    /// Temporary shim until the Blockcore dependency is removed.
    /// </summary>
    public static AngorKey From(BlockcoreKey blockcoreKey)
    {
        var bytes = blockcoreKey.ToBytes();
        try
        {
            return new AngorKey(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }
}
