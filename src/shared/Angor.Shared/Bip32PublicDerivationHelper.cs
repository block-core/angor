using System;
using System.Security.Cryptography;
using NBitcoin;
using NBitcoin.Secp256k1;

namespace Angor.Shared;

/// <summary>
/// Workaround for a .NET 10 ARM64 Android JIT bug where non-hardened public child
/// derivation (<see cref="ExtPubKey.Derive(uint)"/>, BIP32 CKDpub) inside NBitcoin.dll
/// returns the same child key regardless of the derivation index. This collapsed all
/// founder project identifiers on Android (every DeriveAngorKey slot produced the same
/// angor1... id), so founder project scans could never find real projects on mobile.
///
/// Same bug class as <c>TaprootKeyHelper</c> (see docs/ai-docs/taproot-arm64-jit-bug.md):
/// the EC tweak-add path is miscompiled when the call site lives in the net6.0
/// NBitcoin assembly, but produces correct results when the identical primitives are
/// invoked from Angor's own assembly. This helper replicates CKDpub here.
///
/// TODO: Remove once NBitcoin ships a net8.0 TFM build that avoids the Mono/ARM64 JIT
/// bug (tracked with the TaprootKeyHelper removal in GitHub issue #838).
/// </summary>
public static class Bip32PublicDerivationHelper
{
    /// <summary>
    /// BIP32 CKDpub: derives the non-hardened child public key
    /// <c>K_i = point(parse256(I_L)) + K_par</c> with
    /// <c>I = HMAC-SHA512(chainCode, serP(K_par) || ser32(i))</c>.
    /// Equivalent to <c>parent.Derive(index).PubKey</c>.
    /// </summary>
    public static PubKey DerivePublicChild(ExtPubKey parent, uint index)
    {
        if (index >= 0x80000000)
            throw new ArgumentOutOfRangeException(nameof(index), "Hardened derivation requires the private key");

        var data = new byte[37];
        parent.PubKey.ToBytes().CopyTo(data, 0);
        data[33] = (byte)(index >> 24);
        data[34] = (byte)(index >> 16);
        data[35] = (byte)(index >> 8);
        data[36] = (byte)index;

        using var hmac = new HMACSHA512(parent.ChainCode);
        var i = hmac.ComputeHash(data);

        // I_L tweaks the parent point; I_R (child chain code) is not needed for a leaf.
        var il = new byte[32];
        Array.Copy(i, 0, il, 0, 32);

        if (!ECPubKey.TryCreate(parent.PubKey.ToBytes(), null, out _, out var parentEc) || parentEc is null)
            throw new InvalidOperationException("Invalid parent public key");

        if (!parentEc.TryAddTweak(il, out var childEc) || childEc is null)
            throw new InvalidOperationException("BIP32 public derivation produced an invalid child key (tweak overflow)");

        return new PubKey(childEc.ToBytes());
    }
}
