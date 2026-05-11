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
///
/// TODO: Remove this workaround once NBitcoin is upgraded to 10.0.4+, which ships a
/// net8.0 TFM build that avoids the Mono JIT bug and includes the ComputeTapTweak
/// span-aliasing fix (MetacoSA/NBitcoin#1300). Track removal in GitHub issue #838.
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

        return GetTaprootOutputKeyBytes(internalKey, null);
    }

    /// <summary>
    /// Returns the 32-byte x-only taproot output key for a given internal key and
    /// optional merkle root. Equivalent to
    /// <c>internalKey.GetTaprootFullPubKey(merkleRoot).OutputKey.ToBytes()</c>.
    /// </summary>
    public static byte[] GetTaprootOutputKeyBytes(TaprootInternalPubKey internalKey, uint256? merkleRoot)
    {
        var xBytes = internalKey.ToBytes();

        ECXOnlyPubKey.TryCreate(xBytes, out var xonly);

        // Compute the tap tweak: tagged_hash("TapTweak", internal_key || merkle_root)
        var tweak = internalKey.ComputeTapTweak(merkleRoot);

        // Add the tweak to the internal key: output_key = internal_key + tweak * G
        var tweakedPubKey = xonly!.AddTweak(tweak);
        var outputXonly = tweakedPubKey.ToXOnlyPubKey(out _);

        // Serialize the output key
        var result = new byte[32];
        outputXonly.WriteToSpan(result);
        return result;
    }
}
