using NBitcoin;
using NBitcoin.Secp256k1;

namespace Angor.Shared.Protocol.Scripts;

/// <summary>
/// Workaround for a .NET 10 ARM64 Android JIT bug where
/// <see cref="TaprootFullPubKey.Create"/> / <see cref="PubKey.GetTaprootFullPubKey()"/>
/// produces an all-zero output key. The bug is in ComputeTapTweak reusing the same
/// Span&lt;byte&gt; buffer as both scratch space and output — the .NET 10 ARM64 JIT
/// miscompiles this pattern when the method is in a net6.0 assembly (NBitcoin.dll).
///
/// This helper replicates the BIP341 tap tweak computation entirely from Angor's
/// assembly so the JIT produces correct code.
/// </summary>
public static class TaprootKeyHelper
{
    /// <summary>
    /// Returns the 32-byte x-only taproot output key for a given compressed public key hex.
    /// Equivalent to <c>new PubKey(hex).GetTaprootFullPubKey().ToBytes()</c>.
    /// </summary>
    public static byte[] GetTaprootOutputKeyBytes(string compressedPubKeyHex)
    {
        return GetTaprootOutputKeyBytes(new PubKey(compressedPubKeyHex));
    }

    /// <summary>
    /// Returns the 32-byte x-only taproot output key for a given <see cref="PubKey"/>.
    /// Equivalent to <c>pubkey.GetTaprootFullPubKey().ToBytes()</c>.
    /// </summary>
    public static byte[] GetTaprootOutputKeyBytes(PubKey pubkey)
    {
        // 1. Get the x-only internal key bytes from the compressed pubkey
        //    PubKey.TaprootInternalKey uses ECKey (internal), so we go through the public API:
        //    compressed pubkey bytes[1..33] is the x-coordinate
        var compressedBytes = pubkey.ToBytes();
        var xBytes = new byte[32];
        Array.Copy(compressedBytes, 1, xBytes, 0, 32);

        // 2. Build the internal key and ECXOnlyPubKey from x-only bytes
        var internalKey = new TaprootInternalPubKey(xBytes);
        ECXOnlyPubKey.TryCreate(xBytes, out var xonly);

        // 3. Compute the tap tweak: tagged_hash("TapTweak", internal_key)
        //    ComputeTapTweak uses stackalloc internally and returns a new byte[]
        var tweak = internalKey.ComputeTapTweak(null);

        // 4. Add the tweak to the internal key: output_key = internal_key + tweak * G
        var tweakedPubKey = xonly!.AddTweak(tweak);
        var outputXonly = tweakedPubKey.ToXOnlyPubKey(out _);

        // 5. Serialize the output key
        var result = new byte[32];
        outputXonly.WriteToSpan(result);
        return result;
    }
}
