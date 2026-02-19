using System.Security.Cryptography;
using NBitcoin;
using NBitcoin.Secp256k1;
using Microsoft.Extensions.Logging;
using CryptoSHA256 = System.Security.Cryptography.SHA256;

namespace Angor.Sdk.Integration.Lightning;

/// <summary>
/// MuSig2 implementation for Boltz cooperative claiming.
///
/// Implements BIP-327 key aggregation and signing protocol required
/// for Taproot key path spending with Boltz.
///
/// Key handling:
/// - Boltz v2 API uses compressed public keys (33 bytes, 02/03 prefix)
/// - Keys are used with their actual parity for KeyAgg (BIP-327 uses compressed keys)
/// - Keys are sorted by compressed bytes for deterministic ordering (Boltz convention)
/// - Taproot tweak is applied for key-path spending of swap outputs
/// - Per BIP-327, individual key negation is NOT applied; only aggregate-level negation
///
/// BIP-327 uses 33-byte compressed individual public keys (with 02/03 prefix)
/// for all hashing operations.
/// </summary>
public class BoltzMusig2
{
    private readonly ILogger _logger;
    private readonly ECPrivKey _privateKey;
    private readonly ECPubKey _ourPublicKey;       // Actual public key (may have odd Y)
    private readonly ECPubKey _boltzPublicKey;      // Boltz's key as provided (compressed)
    private readonly ECPubKey _aggregatedPubKey;    // Q = KeyAgg result (before tweak)

    // Store the key order and L hash used during aggregation for consistency
    private readonly List<byte[]> _orderedCompressedKeys;
    private readonly byte[] _keyAggLHash;

    // MuSig2 session state - BIP-327 uses two nonce pairs (k1, k2) -> (R1, R2)
    private byte[]? _ourSecNonce1;
    private byte[]? _ourSecNonce2;
    private ECPubKey? _ourPubNonce1;
    private ECPubKey? _ourPubNonce2;
    private ECPubKey? _aggregatedNonce;  // Final R (after binding factor)
    private byte[]? _sessionHash;

    // Nonce aggregation intermediates
    private ECPubKey? _aggR1;
    private ECPubKey? _aggR2;

    // Taproot tweak state
    private bool _hasTaprootTweak;
    private Scalar _tweakScalar;            // t = TaggedHash("TapTweak", Q || merkleRoot)
    private ECPubKey? _tweakedAggPubKey;    // P = g*Q + t*G (the output key)
    private int _gaccSign = 1;              // +1 or -1 accumulated negation from tweak

    public BoltzMusig2(
        byte[] privateKeyBytes,
        byte[] boltzPublicKeyBytes,
        ILogger logger)
    {
        _logger = logger;

        // Parse private key
        if (!ECPrivKey.TryCreate(privateKeyBytes, out var privKey) || privKey == null)
        {
            throw new ArgumentException("Invalid private key");
        }
        _privateKey = privKey;

        // Get our actual public key - use it as-is with its natural parity.
        // BIP-327 uses compressed keys (33 bytes) for all operations. 
        // Individual key negation is NOT done - only aggregate-level negation matters.
        _ourPublicKey = _privateKey.CreatePubKey();

        // Parse Boltz public key - use as-is (compressed, with its actual 02/03 prefix)
        _boltzPublicKey = ParseKey(boltzPublicKeyBytes);

        // Aggregate the public keys using BIP-327 KeyAgg with sorted compressed keys
        (_aggregatedPubKey, _orderedCompressedKeys, _keyAggLHash) =
            AggregatePublicKeysWithOrder(_boltzPublicKey, _ourPublicKey);

        _logger.LogDebug(
            "MuSig2 initialized - Our pubkey: {OurKey}, Boltz pubkey: {BoltzKey}, Aggregated Q: {AggKey}",
            Convert.ToHexString(_ourPublicKey.ToBytes()),
            Convert.ToHexString(_boltzPublicKey.ToBytes()),
            Convert.ToHexString(_aggregatedPubKey.ToBytes()));
    }

    /// <summary>
    /// Set the Taproot tweak for key-path spending.
    /// Must be called before InitializeSession.
    ///
    /// The tweak is: t = TaggedHash("TapTweak", xonly(Q) || merkleRoot)
    /// The tweaked output key is: P = g*Q + t*G
    /// where g = 1 if Q has even Y, else -1
    /// </summary>
    /// <param name="merkleRoot">The merkle root of the Taproot script tree (32 bytes)</param>
    public void SetTaprootTweak(byte[] merkleRoot)
    {
        if (merkleRoot.Length != 32)
            throw new ArgumentException("Merkle root must be 32 bytes");

        _hasTaprootTweak = true;

        // t = TaggedHash("TapTweak", xonly(Q) || merkleRoot)
        var qXOnly = GetXOnlyAggregatedPubKey();
        var tweakBytes = ComputeTaggedHash("TapTweak", qXOnly, merkleRoot);
        _tweakScalar = new Scalar(tweakBytes, out var overflow);

        if (overflow != 0)
        {
            _logger.LogWarning("Tweak scalar overflowed - extremely rare edge case");
        }

        // BIP-327 apply_tweak with is_xonly=true:
        // g = 1 if has_even_y(Q) else n-1
        var qBytes = _aggregatedPubKey.ToBytes();
        bool qHasOddY = qBytes[0] == 0x03;
        _gaccSign = qHasOddY ? -1 : 1;

        // P = g*Q + t*G
        // g*Q: if Q has odd Y, negate Q
        var gQ = qHasOddY ? NegateKey(_aggregatedPubKey) : _aggregatedPubKey;

        // t*G via temporary private key
        if (!ECPrivKey.TryCreate(tweakBytes, out var tweakKey) || tweakKey == null)
        {
            throw new InvalidOperationException("Failed to create tweak key - tweak may be zero or >= group order");
        }
        var tG = tweakKey.CreatePubKey();

        // P = gQ + tG
        _tweakedAggPubKey = AddPubKeys(gQ, tG);

        _logger.LogDebug("Taproot tweak applied. Q: {Q}, P: {P}, tweak: {T}, gacc: {Gacc}",
            Convert.ToHexString(qXOnly),
            Convert.ToHexString(GetXOnlyBytes(_tweakedAggPubKey)),
            Convert.ToHexString(tweakBytes),
            _gaccSign);
    }

    /// <summary>
    /// Get the x-only output public key (32 bytes).
    /// Returns the tweaked key P if a Taproot tweak is set, otherwise the aggregate Q.
    /// </summary>
    public byte[] GetOutputPubKeyXOnly()
    {
        var key = _hasTaprootTweak && _tweakedAggPubKey != null ? _tweakedAggPubKey : _aggregatedPubKey;
        return GetXOnlyBytes(key);
    }

    /// <summary>
    /// Get the aggregated public key Q (33-byte compressed, before tweak)
    /// </summary>
    public byte[] GetAggregatedPubKey() => _aggregatedPubKey.ToBytes();

    /// <summary>
    /// Get the x-only aggregated public key (32 bytes, no prefix) - the internal key before tweak
    /// </summary>
    public byte[] GetXOnlyAggregatedPubKey() => GetXOnlyBytes(_aggregatedPubKey);

    #region Static KeyAgg (BIP-327 compliant, input order)

    /// <summary>
    /// Static method to aggregate multiple public keys using BIP-327 KeyAgg.
    /// Accepts both 33-byte compressed and 32-byte x-only keys.
    /// Keys are used in INPUT order per BIP-327 specification (no sorting).
    /// X-only keys are lifted to even-Y (02 prefix).
    /// </summary>
    /// <returns>X-only aggregated public key (32 bytes)</returns>
    public static byte[] KeyAgg(params byte[][] pubKeys)
    {
        if (pubKeys.Length < 2)
            throw new ArgumentException("At least 2 public keys required for aggregation");

        var compressedKeys = new List<byte[]>();
        var ecPubKeys = new List<ECPubKey>();

        foreach (var key in pubKeys)
        {
            byte[] compressed;
            if (key.Length == 33)
            {
                compressed = key;
            }
            else if (key.Length == 32)
            {
                compressed = new byte[33];
                compressed[0] = 0x02;
                Array.Copy(key, 0, compressed, 1, 32);
            }
            else
            {
                throw new ArgumentException($"Invalid key length: {key.Length}. Expected 32 (x-only) or 33 (compressed).");
            }

            if (!ECPubKey.TryCreate(compressed, Context.Instance, out _, out var pk) || pk == null)
                throw new ArgumentException("Invalid public key");

            compressedKeys.Add(compressed);
            ecPubKeys.Add(pk);
        }

        var aggregated = KeyAggInternal(compressedKeys, ecPubKeys);
        return GetXOnlyBytes(aggregated);
    }

    /// <summary>
    /// Sorted KeyAgg for Boltz use case. Both keys parsed as-is (compressed), then sorted.
    /// X-only keys (32 bytes) are lifted to even-Y (02 prefix) per BIP-327.
    /// Returns x-only aggregate key (32 bytes).
    /// </summary>
    public static byte[] KeyAggSorted(byte[] key1, byte[] key2)
    {
        var pk1 = ParseKeyStatic(key1);
        var pk2 = ParseKeyStatic(key2);

        var compressed1 = pk1.ToBytes();
        var compressed2 = pk2.ToBytes();

        List<byte[]> compressedKeys;
        List<ECPubKey> ecPubKeys;
        if (CompareBytes(compressed1, compressed2) <= 0)
        {
            compressedKeys = new List<byte[]> { compressed1, compressed2 };
            ecPubKeys = new List<ECPubKey> { pk1, pk2 };
        }
        else
        {
            compressedKeys = new List<byte[]> { compressed2, compressed1 };
            ecPubKeys = new List<ECPubKey> { pk2, pk1 };
        }

        var aggregated = KeyAggInternal(compressedKeys, ecPubKeys);
        return GetXOnlyBytes(aggregated);
    }

    /// <summary>
    /// Debug version of KeyAgg that returns intermediate values.
    /// Uses keys in INPUT order per BIP-327.
    /// </summary>
    public static (byte[] Q, byte[] L, byte[]? pk2, List<(byte[] pk, byte[] coeff)> coefficients) KeyAggDebug(
        params byte[][] pubKeys)
    {
        var compressedKeys = new List<byte[]>();
        var ecPubKeys = new List<ECPubKey>();

        foreach (var key in pubKeys)
        {
            byte[] compressed;
            if (key.Length == 33)
                compressed = key;
            else if (key.Length == 32)
            {
                compressed = new byte[33];
                compressed[0] = 0x02;
                Array.Copy(key, 0, compressed, 1, 32);
            }
            else
                throw new ArgumentException($"Invalid key length: {key.Length}");

            ECPubKey.TryCreate(compressed, Context.Instance, out _, out var pk);
            compressedKeys.Add(compressed);
            ecPubKeys.Add(pk!);
        }

        var lHash = ComputeKeyListHash(compressedKeys);

        byte[]? pk2 = null;
        if (compressedKeys.Count >= 1)
        {
            var firstKey = compressedKeys[0];
            for (int i = 1; i < compressedKeys.Count; i++)
            {
                if (CompareBytes(compressedKeys[i], firstKey) != 0)
                {
                    pk2 = compressedKeys[i];
                    break;
                }
            }
        }

        var coefficients = new List<(byte[] pk, byte[] coeff)>();
        ECPubKey? Q = null;
        for (int i = 0; i < ecPubKeys.Count; i++)
        {
            Scalar coeff;
            byte[] coeffBytes;
            if (pk2 != null && CompareBytes(compressedKeys[i], pk2) == 0)
            {
                coeff = new Scalar(1);
                coeffBytes = new byte[32];
                coeffBytes[31] = 1;
            }
            else
            {
                coeff = ComputeKeyAggCoeff(lHash, compressedKeys[i]);
                coeffBytes = new byte[32];
                coeff.WriteToSpan(coeffBytes);
            }
            coefficients.Add((compressedKeys[i], coeffBytes));

            var term = MultiplyPubKey(ecPubKeys[i], coeff);
            Q = Q == null ? term : AddPubKeys(Q, term);
        }

        return (GetXOnlyBytes(Q!), lHash, pk2, coefficients);
    }

    #endregion

    #region Nonce Generation and Aggregation

    /// <summary>
    /// Generate our public nonce for the MuSig2 session.
    /// BIP-327 MuSig2 uses two nonce points (R1, R2) combined into a 66-byte public nonce.
    /// </summary>
    public byte[] GenerateNonce()
    {
        // Generate two secret nonces (retry until valid)
        _ourSecNonce1 = RandomNumberGenerator.GetBytes(32);
        while (!ECPrivKey.TryCreate(_ourSecNonce1, out _))
            _ourSecNonce1 = RandomNumberGenerator.GetBytes(32);

        _ourSecNonce2 = RandomNumberGenerator.GetBytes(32);
        while (!ECPrivKey.TryCreate(_ourSecNonce2, out _))
            _ourSecNonce2 = RandomNumberGenerator.GetBytes(32);

        ECPrivKey.TryCreate(_ourSecNonce1, out var noncePriv1);
        _ourPubNonce1 = noncePriv1!.CreatePubKey();

        ECPrivKey.TryCreate(_ourSecNonce2, out var noncePriv2);
        _ourPubNonce2 = noncePriv2!.CreatePubKey();

        // 66-byte public nonce: R1 (33 bytes) || R2 (33 bytes)
        var pubNonceBytes = new byte[66];
        Array.Copy(_ourPubNonce1.ToBytes(), 0, pubNonceBytes, 0, 33);
        Array.Copy(_ourPubNonce2.ToBytes(), 0, pubNonceBytes, 33, 33);

        _logger.LogDebug("Generated MuSig2 nonce (66 bytes): R1={R1}, R2={R2}",
            Convert.ToHexString(_ourPubNonce1.ToBytes()),
            Convert.ToHexString(_ourPubNonce2.ToBytes()));

        return pubNonceBytes;
    }

    /// <summary>
    /// Aggregate our nonce with Boltz's nonce.
    /// aggR_j = R_j_ours + R_j_boltz for j in {1, 2}
    /// Final R is computed in InitializeSession with the binding factor.
    /// </summary>
    public void AggregateNonces(byte[] boltzPubNonceBytes)
    {
        if (_ourPubNonce1 == null || _ourPubNonce2 == null)
            throw new InvalidOperationException("Must call GenerateNonce first");

        ECPubKey boltzR1, boltzR2;

        if (boltzPubNonceBytes.Length == 66)
        {
            var r1Bytes = new byte[33];
            Array.Copy(boltzPubNonceBytes, 0, r1Bytes, 0, 33);
            if (!ECPubKey.TryCreate(r1Bytes, Context.Instance, out _, out var r1) || r1 == null)
                throw new ArgumentException("Invalid Boltz nonce R1");
            boltzR1 = r1;

            var r2Bytes = new byte[33];
            Array.Copy(boltzPubNonceBytes, 33, r2Bytes, 0, 33);
            if (!ECPubKey.TryCreate(r2Bytes, Context.Instance, out _, out var r2) || r2 == null)
                throw new ArgumentException("Invalid Boltz nonce R2");
            boltzR2 = r2;
        }
        else if (boltzPubNonceBytes.Length == 33)
        {
            if (!ECPubKey.TryCreate(boltzPubNonceBytes, Context.Instance, out _, out var parsed) || parsed == null)
                throw new ArgumentException("Invalid Boltz public nonce");
            boltzR1 = parsed;
            boltzR2 = parsed;
            _logger.LogWarning("Received 33-byte nonce from Boltz, expected 66 bytes");
        }
        else
        {
            throw new ArgumentException($"Invalid Boltz nonce length: {boltzPubNonceBytes.Length}, expected 66");
        }

        _aggR1 = AddPubKeys(_ourPubNonce1, boltzR1);
        _aggR2 = AddPubKeys(_ourPubNonce2, boltzR2);

        _logger.LogDebug("Aggregated nonces - aggR1: {AggR1}, aggR2: {AggR2}",
            Convert.ToHexString(_aggR1.ToBytes()),
            Convert.ToHexString(_aggR2.ToBytes()));
    }

    /// <summary>
    /// Initialize the signing session with the message (sighash).
    /// Computes the final R using the binding factor b.
    /// BIP-327: b = H("MuSig/noncecoef" || aggnonce || xbytes(Q) || msg)
    /// where Q is the FINAL key (tweaked P if applicable).
    /// R = aggR1 + b * aggR2
    /// </summary>
    public void InitializeSession(byte[] sighash)
    {
        _sessionHash = sighash;

        if (_aggR1 != null && _aggR2 != null)
        {
            var effectiveKey = _hasTaprootTweak && _tweakedAggPubKey != null
                ? _tweakedAggPubKey
                : _aggregatedPubKey;

            var b = ComputeNonceBindingFactor(_aggR1, _aggR2, effectiveKey, sighash);
            var bTimesR2 = MultiplyPubKey(_aggR2, b);
            _aggregatedNonce = AddPubKeys(_aggR1, bTimesR2);

            _logger.LogDebug("Final aggregated nonce R: {R}", Convert.ToHexString(_aggregatedNonce.ToBytes()));
        }

        _logger.LogDebug("MuSig2 session initialized with sighash: {Sighash}", Convert.ToHexString(sighash));
    }

    #endregion

    #region Signing

    /// <summary>
    /// Create our partial signature.
    /// BIP-327: s = k + e*a*d (mod n) where d = g * gacc * d'
    /// - d' = raw private key, negated if our pubkey was lifted to even-Y
    /// - gacc from tweak application
    /// - g from final aggregate key parity
    /// - e = challenge using FINAL key (tweaked if applicable)
    /// - a = KeyAgg coefficient for our key
    /// </summary>
    public byte[] SignPartial()
    {
        if (_ourSecNonce1 == null || _ourSecNonce2 == null || _sessionHash == null || _aggregatedNonce == null)
            throw new InvalidOperationException("Session not properly initialized");

        // Effective key for challenge: P (tweaked) if tweak set, else Q
        var effectiveKey = _hasTaprootTweak && _tweakedAggPubKey != null
            ? _tweakedAggPubKey
            : _aggregatedPubKey;

        // Challenge: e = H("BIP0340/challenge" || xonly(R) || xonly(Q_final) || msg)
        var challenge = ComputeChallenge(_aggregatedNonce, effectiveKey, _sessionHash);

        // Binding factor (same as InitializeSession)
        var b = _aggR1 != null && _aggR2 != null
            ? ComputeNonceBindingFactor(_aggR1, _aggR2, effectiveKey, _sessionHash)
            : new Scalar(1);

        // Nonce: k = k1 + b*k2
        var k1 = new Scalar(_ourSecNonce1, out _);
        var k2 = new Scalar(_ourSecNonce2, out _);
        var k = k1.Add(b.Multiply(k2));

        // Negate k if R has odd Y
        var rBytes = _aggregatedNonce.ToBytes();
        if (rBytes[0] == 0x03)
        {
            k = k.Negate();
            _logger.LogDebug("Negated k (R has odd Y)");
        }

        // Private key d' (BIP-327: raw scalar, NO individual key negation)
        var xBytes = new byte[32];
        _privateKey.WriteToSpan(xBytes);
        var dPrime = new Scalar(xBytes, out _);

        // BIP-327: d = g * gacc * d'
        // g = 1 if has_even_y(Q_final) else -1
        var pBytes = effectiveKey.ToBytes();
        if (pBytes[0] == 0x03) // g = -1
        {
            dPrime = dPrime.Negate();
            _logger.LogDebug("Negated d for g (final key has odd Y)");
        }

        // gacc from tweak (only relevant when tweak is active)
        if (_hasTaprootTweak && _gaccSign == -1)
        {
            dPrime = dPrime.Negate();
            _logger.LogDebug("Negated d for gacc (Q had odd Y before tweak)");
        }

        // KeyAgg coefficient for our key
        var ourKeyBytes = _ourPublicKey.ToBytes();
        byte[]? pk2 = FindSecondKey();
        Scalar a;
        if (pk2 != null && CompareBytes(ourKeyBytes, pk2) == 0)
        {
            a = new Scalar(1);
            _logger.LogDebug("Our key is pk2, coefficient = 1");
        }
        else
        {
            a = ComputeKeyAggCoeff(_keyAggLHash, ourKeyBytes);
            _logger.LogDebug("Using computed coefficient for our key");
        }

        // Partial signature: s = k + e*a*d
        var e = new Scalar(challenge, out _);
        var partialSig = k.Add(e.Multiply(a).Multiply(dPrime));

        var result = new byte[32];
        partialSig.WriteToSpan(result);

        _logger.LogDebug("SignPartial - R odd: {ROdd}, P odd: {POdd}, gacc: {Gacc}",
            rBytes[0] == 0x03, pBytes[0] == 0x03, _gaccSign);
        _logger.LogDebug("Partial signature: {Sig}", Convert.ToHexString(result));

        return result;
    }

    /// <summary>
    /// Aggregate partial signatures into final 64-byte Schnorr signature.
    /// BIP-327: s = sum(s_i) + e * g * tacc
    /// </summary>
    public byte[] AggregatePartials(byte[] boltzPartialSig, byte[] ourPartialSig)
    {
        if (_aggregatedNonce == null || _sessionHash == null)
            throw new InvalidOperationException("Session not initialized");

        var s1 = new Scalar(boltzPartialSig, out _);
        var s2 = new Scalar(ourPartialSig, out _);
        var s = s1.Add(s2);

        // Add tweak contribution: s += e * g * tacc
        if (_hasTaprootTweak)
        {
            var effectiveKey = _tweakedAggPubKey ?? _aggregatedPubKey;
            var challenge = ComputeChallenge(_aggregatedNonce, effectiveKey, _sessionHash);
            var e = new Scalar(challenge, out _);

            // g = 1 if P has even Y, else -1
            var pBytes = effectiveKey.ToBytes();
            var tweakContrib = _tweakScalar;
            if (pBytes[0] == 0x03)
                tweakContrib = tweakContrib.Negate();

            s = s.Add(e.Multiply(tweakContrib));
            _logger.LogDebug("Added tweak contribution (P odd Y: {Odd})", pBytes[0] == 0x03);
        }

        // 64-byte Schnorr signature: xonly(R) || s
        var signature = new byte[64];
        var rBytes = _aggregatedNonce.ToBytes();
        Array.Copy(rBytes, 1, signature, 0, 32);
        s.WriteToSpan(signature.AsSpan(32));

        _logger.LogDebug("Aggregated signature: {Sig}", Convert.ToHexString(signature));
        return signature;
    }

    #endregion

    #region Private Helpers

    private byte[]? FindSecondKey()
    {
        if (_orderedCompressedKeys.Count < 2) return null;
        var firstKey = _orderedCompressedKeys[0];
        for (int i = 1; i < _orderedCompressedKeys.Count; i++)
        {
            if (CompareBytes(_orderedCompressedKeys[i], firstKey) != 0)
                return _orderedCompressedKeys[i];
        }
        return null;
    }

    private static ECPubKey ParseKey(byte[] keyBytes) => ParseKeyStatic(keyBytes);

    /// <summary>
    /// Parse a public key from bytes. Accepts 33-byte compressed or 32-byte x-only.
    /// X-only keys are lifted to even-Y (02 prefix) per BIP-327.
    /// Compressed keys are used as-is with their actual parity.
    /// </summary>
    private static ECPubKey ParseKeyStatic(byte[] keyBytes)
    {
        if (keyBytes.Length == 33)
        {
            if (!ECPubKey.TryCreate(keyBytes, Context.Instance, out _, out var pk) || pk == null)
                throw new ArgumentException("Invalid public key");
            return pk;
        }
        else if (keyBytes.Length == 32)
        {
            var compressed = new byte[33];
            compressed[0] = 0x02; // x-only → lift to even-Y per BIP-327
            Array.Copy(keyBytes, 0, compressed, 1, 32);
            if (!ECPubKey.TryCreate(compressed, Context.Instance, out _, out var pk) || pk == null)
                throw new ArgumentException("Invalid public key (x-only)");
            return pk;
        }
        else
        {
            throw new ArgumentException($"Invalid public key length: {keyBytes.Length}");
        }
    }

    private static ECPubKey NormalizeToEvenY(ECPubKey key)
    {
        var compressed = key.ToBytes();
        if (compressed[0] == 0x02) return key;
        compressed[0] = 0x02;
        ECPubKey.TryCreate(compressed, Context.Instance, out _, out var evenYKey);
        return evenYKey!;
    }

    private static ECPubKey NegateKey(ECPubKey key)
    {
        var compressed = (byte[])key.ToBytes().Clone();
        compressed[0] = compressed[0] == 0x02 ? (byte)0x03 : (byte)0x02;
        ECPubKey.TryCreate(compressed, Context.Instance, out _, out var negated);
        return negated!;
    }

    private static ECPubKey KeyAggInternal(List<byte[]> compressedKeys, List<ECPubKey> ecPubKeys)
    {
        var lHash = ComputeKeyListHash(compressedKeys);

        byte[]? pk2 = null;
        if (compressedKeys.Count >= 1)
        {
            var firstKey = compressedKeys[0];
            for (int i = 1; i < compressedKeys.Count; i++)
            {
                if (CompareBytes(compressedKeys[i], firstKey) != 0)
                {
                    pk2 = compressedKeys[i];
                    break;
                }
            }
        }

        ECPubKey? Q = null;
        for (int i = 0; i < ecPubKeys.Count; i++)
        {
            Scalar coeff = (pk2 != null && CompareBytes(compressedKeys[i], pk2) == 0)
                ? new Scalar(1)
                : ComputeKeyAggCoeff(lHash, compressedKeys[i]);

            var term = MultiplyPubKey(ecPubKeys[i], coeff);
            Q = Q == null ? term : AddPubKeys(Q, term);
        }
        return Q!;
    }

    private static byte[] ComputeKeyListHash(List<byte[]> keys)
    {
        var tagHash = CryptoSHA256.HashData(System.Text.Encoding.UTF8.GetBytes("KeyAgg list"));
        var totalLen = tagHash.Length * 2;
        foreach (var key in keys) totalLen += key.Length;

        var data = new byte[totalLen];
        var offset = 0;
        Array.Copy(tagHash, 0, data, offset, tagHash.Length); offset += tagHash.Length;
        Array.Copy(tagHash, 0, data, offset, tagHash.Length); offset += tagHash.Length;
        foreach (var key in keys) { Array.Copy(key, 0, data, offset, key.Length); offset += key.Length; }

        return CryptoSHA256.HashData(data);
    }

    private static Scalar ComputeKeyAggCoeff(byte[] lHash, byte[] pkBytes)
    {
        var tagHash = CryptoSHA256.HashData(System.Text.Encoding.UTF8.GetBytes("KeyAgg coefficient"));
        var data = new byte[tagHash.Length * 2 + lHash.Length + pkBytes.Length];
        var offset = 0;
        Array.Copy(tagHash, 0, data, offset, tagHash.Length); offset += tagHash.Length;
        Array.Copy(tagHash, 0, data, offset, tagHash.Length); offset += tagHash.Length;
        Array.Copy(lHash, 0, data, offset, lHash.Length); offset += lHash.Length;
        Array.Copy(pkBytes, 0, data, offset, pkBytes.Length);
        return new Scalar(CryptoSHA256.HashData(data), out _);
    }

    private static Scalar ComputeNonceBindingFactor(ECPubKey aggR1, ECPubKey aggR2, ECPubKey aggPubKey, byte[] message)
    {
        var tagHash = CryptoSHA256.HashData(System.Text.Encoding.UTF8.GetBytes("MuSig/noncecoef"));
        var r1Bytes = aggR1.ToBytes();
        var r2Bytes = aggR2.ToBytes();
        var qXOnly = GetXOnlyBytes(aggPubKey);

        var data = new byte[tagHash.Length * 2 + 66 + 32 + message.Length];
        var offset = 0;
        Array.Copy(tagHash, 0, data, offset, tagHash.Length); offset += tagHash.Length;
        Array.Copy(tagHash, 0, data, offset, tagHash.Length); offset += tagHash.Length;
        Array.Copy(r1Bytes, 0, data, offset, r1Bytes.Length); offset += r1Bytes.Length;
        Array.Copy(r2Bytes, 0, data, offset, r2Bytes.Length); offset += r2Bytes.Length;
        Array.Copy(qXOnly, 0, data, offset, qXOnly.Length); offset += qXOnly.Length;
        Array.Copy(message, 0, data, offset, message.Length);

        return new Scalar(CryptoSHA256.HashData(data), out _);
    }

    private static byte[] ComputeChallenge(ECPubKey nonce, ECPubKey pubkey, byte[] message)
    {
        var tag = CryptoSHA256.HashData(System.Text.Encoding.UTF8.GetBytes("BIP0340/challenge"));
        var data = new byte[tag.Length * 2 + 32 + 32 + message.Length];
        var offset = 0;
        Array.Copy(tag, 0, data, offset, tag.Length); offset += tag.Length;
        Array.Copy(tag, 0, data, offset, tag.Length); offset += tag.Length;
        Array.Copy(nonce.ToBytes(), 1, data, offset, 32); offset += 32;
        Array.Copy(pubkey.ToBytes(), 1, data, offset, 32); offset += 32;
        Array.Copy(message, 0, data, offset, message.Length);
        return CryptoSHA256.HashData(data);
    }

    private static byte[] ComputeTaggedHash(string tag, params byte[][] parts)
    {
        var tagHash = CryptoSHA256.HashData(System.Text.Encoding.UTF8.GetBytes(tag));
        var totalLen = tagHash.Length * 2;
        foreach (var p in parts) totalLen += p.Length;

        var data = new byte[totalLen];
        var offset = 0;
        Array.Copy(tagHash, 0, data, offset, tagHash.Length); offset += tagHash.Length;
        Array.Copy(tagHash, 0, data, offset, tagHash.Length); offset += tagHash.Length;
        foreach (var p in parts) { Array.Copy(p, 0, data, offset, p.Length); offset += p.Length; }

        return CryptoSHA256.HashData(data);
    }

    private static ECPubKey MultiplyPubKey(ECPubKey pk, Scalar scalar)
    {
        var resultGej = pk.Q.MultConst(scalar, 256);
        return new ECPubKey(resultGej.ToGroupElement(), Context.Instance);
    }

    private static ECPubKey AddPubKeys(ECPubKey pk1, ECPubKey pk2)
    {
        var sum = pk1.Q.ToGroupElementJacobian().AddVariable(pk2.Q);
        return new ECPubKey(sum.ToGroupElement(), Context.Instance);
    }

    private static byte[] GetXOnlyBytes(ECPubKey pk)
    {
        var full = pk.ToBytes();
        var xOnly = new byte[32];
        Array.Copy(full, 1, xOnly, 0, 32);
        return xOnly;
    }

    private static int CompareBytes(byte[] a, byte[] b)
    {
        for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            if (a[i] != b[i]) return a[i].CompareTo(b[i]);
        }
        return a.Length.CompareTo(b.Length);
    }

    private static (ECPubKey aggregatedKey, List<byte[]> orderedKeys, byte[] lHash) AggregatePublicKeysWithOrder(
        ECPubKey pk1, ECPubKey pk2)
    {
        var compressed1 = pk1.ToBytes();
        var compressed2 = pk2.ToBytes();

        List<byte[]> compressedKeys;
        List<ECPubKey> ecPubKeys;
        if (CompareBytes(compressed1, compressed2) <= 0)
        {
            compressedKeys = new List<byte[]> { compressed1, compressed2 };
            ecPubKeys = new List<ECPubKey> { pk1, pk2 };
        }
        else
        {
            compressedKeys = new List<byte[]> { compressed2, compressed1 };
            ecPubKeys = new List<ECPubKey> { pk2, pk1 };
        }

        var lHash = ComputeKeyListHash(compressedKeys);
        var aggregated = KeyAggInternal(compressedKeys, ecPubKeys);
        return (aggregated, compressedKeys, lHash);
    }

    #endregion
}
