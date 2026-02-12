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
/// BIP-327 uses 33-byte compressed individual public keys (with 02/03 prefix)
/// for all hashing operations. Keys are processed in INPUT order (no sorting).
/// </summary>
public class BoltzMusig2
{
    private readonly ILogger _logger;
    private readonly ECPrivKey _privateKey;
    private readonly ECPubKey _ourPublicKey;
    private readonly ECPubKey _boltzPublicKey;
    private readonly ECPubKey _aggregatedPubKey;
    private readonly byte[] _sessionId;
    
    // Store the key order and L hash used during aggregation for consistency
    private readonly List<byte[]> _orderedCompressedKeys;
    private readonly byte[] _keyAggLHash;

    // MuSig2 session state - BIP-327 uses two nonce pairs (k1, k2) -> (R1, R2)
    private byte[]? _ourSecNonce1;
    private byte[]? _ourSecNonce2;
    private ECPubKey? _ourPubNonce1;
    private ECPubKey? _ourPubNonce2;
    private ECPubKey? _aggregatedNonce;
    private byte[]? _sessionHash;

    public BoltzMusig2(
        byte[] privateKeyBytes,
        byte[] boltzPublicKeyBytes,
        ILogger logger)
    {
        _logger = logger;
        _sessionId = RandomNumberGenerator.GetBytes(32);

        // Parse private key
        if (!ECPrivKey.TryCreate(privateKeyBytes, out var privKey) || privKey == null)
        {
            throw new ArgumentException("Invalid private key");
        }
        _privateKey = privKey;
        _ourPublicKey = _privateKey.CreatePubKey();

        // Parse Boltz public key (handle both 33-byte compressed and 32-byte x-only)
        if (boltzPublicKeyBytes.Length == 33)
        {
            if (!ECPubKey.TryCreate(boltzPublicKeyBytes, Context.Instance, out _, out var pubKey) || pubKey == null)
            {
                throw new ArgumentException("Invalid Boltz public key");
            }
            _boltzPublicKey = pubKey;
        }
        else if (boltzPublicKeyBytes.Length == 32)
        {
            // X-only format - add 02 prefix (lift_x: always even y)
            var compressedBytes = new byte[33];
            compressedBytes[0] = 0x02;
            Array.Copy(boltzPublicKeyBytes, 0, compressedBytes, 1, 32);
            if (!ECPubKey.TryCreate(compressedBytes, Context.Instance, out _, out var pubKey) || pubKey == null)
            {
                throw new ArgumentException("Invalid Boltz public key (x-only format)");
            }
            _boltzPublicKey = pubKey;
        }
        else
        {
            throw new ArgumentException($"Invalid Boltz public key length: {boltzPublicKeyBytes.Length}");
        }

        // Aggregate the public keys using BIP-327 KeyAgg
        // Store the key order for consistent coefficient computation
        (_aggregatedPubKey, _orderedCompressedKeys, _keyAggLHash) = AggregatePublicKeysWithOrder(_boltzPublicKey, _ourPublicKey);

        _logger.LogDebug("MuSig2 initialized - Our pubkey: {OurKey}, Boltz pubkey: {BoltzKey}, Aggregated: {AggKey}",
            Convert.ToHexString(_ourPublicKey.ToBytes()),
            Convert.ToHexString(_boltzPublicKey.ToBytes()),
            Convert.ToHexString(_aggregatedPubKey.ToBytes()));
    }

    /// <summary>
    /// Get the aggregated public key (for Taproot internal key)
    /// </summary>
    public byte[] GetAggregatedPubKey() => _aggregatedPubKey.ToBytes();

    /// <summary>
    /// Get the x-only aggregated public key (32 bytes, no prefix)
    /// </summary>
    public byte[] GetXOnlyAggregatedPubKey()
    {
        var full = _aggregatedPubKey.ToBytes();
        var xOnly = new byte[32];
        Array.Copy(full, 1, xOnly, 0, 32);
        return xOnly;
    }

    /// <summary>
    /// Static method to aggregate multiple public keys using BIP-327 KeyAgg.
    /// Accepts both 33-byte compressed and 32-byte x-only keys.
    /// Keys are used in INPUT order per BIP-327 specification (no sorting).
    /// </summary>
    /// <param name="pubKeys">List of public keys (33-byte compressed or 32-byte x-only)</param>
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
                // X-only: lift_x (always even y, 02 prefix)
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

        // Aggregate using BIP-327 KeyAgg (input order, no sorting)
        var aggregated = KeyAggInternal(compressedKeys, ecPubKeys);

        // Return x-only format
        var result = aggregated.ToBytes();
        var xOnly = new byte[32];
        Array.Copy(result, 1, xOnly, 0, 32);
        return xOnly;
    }

    /// <summary>
    /// Debug version of KeyAgg that returns intermediate values.
    /// Uses keys in INPUT order per BIP-327.
    /// </summary>
    public static (byte[] Q, byte[] L, byte[]? pk2, List<(byte[] pk, byte[] coeff)> coefficients) KeyAggDebug(params byte[][] pubKeys)
    {
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
                throw new ArgumentException($"Invalid key length: {key.Length}");
            }

            ECPubKey.TryCreate(compressed, Context.Instance, out _, out var pk);
            compressedKeys.Add(compressed);
            ecPubKeys.Add(pk!);
        }

        // BIP-327: Compute L from keys in INPUT order (no sorting), using 33-byte compressed keys
        var lHash = ComputeKeyListHash(compressedKeys);

        // BIP-327 GetSecondKey: first key different from the first key, in INPUT order
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

        // Compute coefficients in INPUT order
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
                coeffBytes[31] = 1; // scalar 1
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

        var qBytes = Q!.ToBytes();
        var qXOnly = new byte[32];
        Array.Copy(qBytes, 1, qXOnly, 0, 32);

        return (qXOnly, lHash, pk2, coefficients);
    }

    /// <summary>
    /// BIP-327 KeyAgg internal implementation.
    /// Q = sum(KeyAggCoeff(pk_list, pk_i) * pk_i) for all i
    ///
    /// Per BIP-327:
    /// - Keys are used in INPUT order (no sorting)
    /// - L = HashKeys(pk_list) using 33-byte compressed keys
    /// - pk2 = GetSecondKey(pk_list) - first key != first key, in input order
    /// - Coefficient for pk2 is 1, all others use hash_agg(L || pk)
    /// </summary>
    private static ECPubKey KeyAggInternal(List<byte[]> compressedKeys, List<ECPubKey> ecPubKeys)
    {
        // Step 1: Compute L = HashKeys(pk_list) - 33-byte compressed keys in INPUT order per BIP-327
        var lHash = ComputeKeyListHash(compressedKeys);

        // Step 2: GetSecondKey - first key different from the first key, in INPUT order
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

        // Step 3: Accumulate Q = sum(a_i * P_i) in INPUT order
        ECPubKey? Q = null;
        for (int i = 0; i < ecPubKeys.Count; i++)
        {
            // Compute coefficient a_i
            Scalar coeff;
            if (pk2 != null && CompareBytes(compressedKeys[i], pk2) == 0)
            {
                // pk2 gets coefficient 1
                coeff = new Scalar(1);
            }
            else
            {
                // All other keys get coefficient hash_agg(L || pk_i)
                coeff = ComputeKeyAggCoeff(lHash, compressedKeys[i]);
            }

            // term = coeff * P_i
            var term = MultiplyPubKey(ecPubKeys[i], coeff);

            if (Q == null)
                Q = term;
            else
                Q = AddPubKeys(Q, term);
        }

        return Q!;
    }

    /// <summary>
    /// Compute L = HashKeys(pk1 || pk2 || ... || pkn)
    /// using tagged hash "KeyAgg list".
    /// Keys should be 33-byte compressed per BIP-327.
    /// </summary>
    private static byte[] ComputeKeyListHash(List<byte[]> keys)
    {
        // Tagged hash: SHA256(SHA256(tag) || SHA256(tag) || data)
        var tagHash = CryptoSHA256.HashData(System.Text.Encoding.UTF8.GetBytes("KeyAgg list"));

        var totalLen = tagHash.Length * 2;
        foreach (var key in keys)
            totalLen += key.Length;

        var data = new byte[totalLen];
        var offset = 0;

        Array.Copy(tagHash, 0, data, offset, tagHash.Length);
        offset += tagHash.Length;
        Array.Copy(tagHash, 0, data, offset, tagHash.Length);
        offset += tagHash.Length;

        foreach (var key in keys)
        {
            Array.Copy(key, 0, data, offset, key.Length);
            offset += key.Length;
        }

        return CryptoSHA256.HashData(data);
    }

    /// <summary>
    /// Compute coefficient hash_agg(L || pk)
    /// using tagged hash "KeyAgg coefficient".
    /// pk should be 33-byte compressed per BIP-327.
    /// </summary>
    private static Scalar ComputeKeyAggCoeff(byte[] lHash, byte[] pkBytes)
    {
        // Tagged hash: SHA256(SHA256(tag) || SHA256(tag) || L || pk)
        var tagHash = CryptoSHA256.HashData(System.Text.Encoding.UTF8.GetBytes("KeyAgg coefficient"));

        var data = new byte[tagHash.Length * 2 + lHash.Length + pkBytes.Length];
        var offset = 0;

        Array.Copy(tagHash, 0, data, offset, tagHash.Length);
        offset += tagHash.Length;
        Array.Copy(tagHash, 0, data, offset, tagHash.Length);
        offset += tagHash.Length;
        Array.Copy(lHash, 0, data, offset, lHash.Length);
        offset += lHash.Length;
        Array.Copy(pkBytes, 0, data, offset, pkBytes.Length);

        var hash = CryptoSHA256.HashData(data);
        return new Scalar(hash, out _);
    }

    /// <summary>
    /// Generate our public nonce for the MuSig2 session.
    /// BIP-327 MuSig2 uses two nonce points (R1, R2) combined into a 66-byte public nonce.
    /// </summary>
    public byte[] GenerateNonce()
    {
        // Generate two secret nonces
        _ourSecNonce1 = RandomNumberGenerator.GetBytes(32);
        _ourSecNonce2 = RandomNumberGenerator.GetBytes(32);

        // Create first nonce key pair (k1 -> R1)
        if (!ECPrivKey.TryCreate(_ourSecNonce1, out var noncePriv1) || noncePriv1 == null)
        {
            throw new InvalidOperationException("Failed to create nonce private key 1");
        }
        _ourPubNonce1 = noncePriv1.CreatePubKey();

        // Create second nonce key pair (k2 -> R2)
        if (!ECPrivKey.TryCreate(_ourSecNonce2, out var noncePriv2) || noncePriv2 == null)
        {
            throw new InvalidOperationException("Failed to create nonce private key 2");
        }
        _ourPubNonce2 = noncePriv2.CreatePubKey();

        // Combine into 66-byte public nonce: R1 (33 bytes) || R2 (33 bytes)
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
    /// BIP-327 nonces are 66 bytes (R1 || R2).
    /// Aggregation: aggR1 = R1_ours + R1_boltz, aggR2 = R2_ours + R2_boltz
    /// Final nonce: R = aggR1 + b * aggR2 (where b is a binding factor)
    /// </summary>
    public void AggregateNonces(byte[] boltzPubNonceBytes)
    {
        if (_ourPubNonce1 == null || _ourPubNonce2 == null)
        {
            throw new InvalidOperationException("Must call GenerateNonce first");
        }

        // Parse Boltz's public nonce (should be 66 bytes: R1 || R2)
        ECPubKey boltzR1, boltzR2;
        
        if (boltzPubNonceBytes.Length == 66)
        {
            // Parse R1 (first 33 bytes)
            var r1Bytes = new byte[33];
            Array.Copy(boltzPubNonceBytes, 0, r1Bytes, 0, 33);
            if (!ECPubKey.TryCreate(r1Bytes, Context.Instance, out _, out var r1) || r1 == null)
            {
                throw new ArgumentException("Invalid Boltz nonce R1");
            }
            boltzR1 = r1;

            // Parse R2 (last 33 bytes)
            var r2Bytes = new byte[33];
            Array.Copy(boltzPubNonceBytes, 33, r2Bytes, 0, 33);
            if (!ECPubKey.TryCreate(r2Bytes, Context.Instance, out _, out var r2) || r2 == null)
            {
                throw new ArgumentException("Invalid Boltz nonce R2");
            }
            boltzR2 = r2;
        }
        else if (boltzPubNonceBytes.Length == 33)
        {
            // Legacy single nonce - use same for R1 and R2
            if (!ECPubKey.TryCreate(boltzPubNonceBytes, Context.Instance, out _, out var parsed) || parsed == null)
            {
                throw new ArgumentException("Invalid Boltz public nonce");
            }
            boltzR1 = parsed;
            boltzR2 = parsed;
            _logger.LogWarning("Received 33-byte nonce from Boltz, expected 66 bytes");
        }
        else
        {
            throw new ArgumentException($"Invalid Boltz nonce length: {boltzPubNonceBytes.Length}, expected 66");
        }

        // Aggregate: aggR1 = R1_ours + R1_boltz
        var aggR1 = AddPubKeys(_ourPubNonce1, boltzR1);
        // Aggregate: aggR2 = R2_ours + R2_boltz
        var aggR2 = AddPubKeys(_ourPubNonce2, boltzR2);

        // For now, use simple aggregation: R = aggR1 (the binding factor computation is complex)
        // In full BIP-327: R = aggR1 + b * aggR2 where b = hash(aggR1 || aggR2 || aggPubKey || msg)
        // For compatibility, we'll compute this properly in SignPartial when we have the message
        _aggregatedNonce = aggR1;
        
        // Store aggR2 for later use in binding factor computation
        _aggR1 = aggR1;
        _aggR2 = aggR2;

        _logger.LogDebug("Aggregated nonces - aggR1: {AggR1}, aggR2: {AggR2}",
            Convert.ToHexString(aggR1.ToBytes()),
            Convert.ToHexString(aggR2.ToBytes()));
    }
    
    private ECPubKey? _aggR1;
    private ECPubKey? _aggR2;

    /// <summary>
    /// Initialize the signing session with the message (sighash)
    /// </summary>
    public void InitializeSession(byte[] sighash)
    {
        _sessionHash = sighash;
        
        // Now compute the final aggregated nonce R = aggR1 + b * aggR2
        // where b = hash("MuSig/noncecoef" || aggR1 || aggR2 || Q || m)
        if (_aggR1 != null && _aggR2 != null)
        {
            var b = ComputeNonceBindingFactor(_aggR1, _aggR2, _aggregatedPubKey, sighash);
            var bTimesR2 = MultiplyPubKey(_aggR2, b);
            _aggregatedNonce = AddPubKeys(_aggR1, bTimesR2);
            
            _logger.LogDebug("Final aggregated nonce R: {R}", Convert.ToHexString(_aggregatedNonce.ToBytes()));
        }
        
        _logger.LogDebug("MuSig2 session initialized with sighash: {Sighash}", Convert.ToHexString(sighash));
    }

    /// <summary>
    /// Compute the nonce binding factor b = H("MuSig/noncecoef" || aggnonce || xbytes(Q) || m)
    /// Per BIP-327:
    /// - aggnonce = R1 (33 bytes) || R2 (33 bytes) = 66 bytes total
    /// - xbytes(Q) = x-only aggregated pubkey (32 bytes)
    /// </summary>
    private static Scalar ComputeNonceBindingFactor(ECPubKey aggR1, ECPubKey aggR2, ECPubKey aggPubKey, byte[] message)
    {
        var tagHash = CryptoSHA256.HashData(System.Text.Encoding.UTF8.GetBytes("MuSig/noncecoef"));
        
        // BIP-327: aggnonce = R1 || R2 (66 bytes compressed)
        var r1Bytes = aggR1.ToBytes(); // 33 bytes
        var r2Bytes = aggR2.ToBytes(); // 33 bytes
        
        // BIP-327: xbytes(Q) = x-only format (32 bytes, no prefix)
        var qFullBytes = aggPubKey.ToBytes();
        var qXOnly = new byte[32];
        Array.Copy(qFullBytes, 1, qXOnly, 0, 32);
        
        // Format: tagged_hash || aggnonce (66) || xbytes(Q) (32) || msg
        var data = new byte[tagHash.Length * 2 + 66 + 32 + message.Length];
        var offset = 0;
        
        Array.Copy(tagHash, 0, data, offset, tagHash.Length); offset += tagHash.Length;
        Array.Copy(tagHash, 0, data, offset, tagHash.Length); offset += tagHash.Length;
        Array.Copy(r1Bytes, 0, data, offset, r1Bytes.Length); offset += r1Bytes.Length; // 33 bytes
        Array.Copy(r2Bytes, 0, data, offset, r2Bytes.Length); offset += r2Bytes.Length; // 33 bytes
        Array.Copy(qXOnly, 0, data, offset, qXOnly.Length); offset += qXOnly.Length;    // 32 bytes
        Array.Copy(message, 0, data, offset, message.Length);
        
        var hash = CryptoSHA256.HashData(data);
        return new Scalar(hash, out _);
    }

    /// <summary>
    /// Create our partial signature.
    /// BIP-327 formula: s = k + e*a*d (mod n)
    /// where:
    /// - k = k1 + b*k2 (effective secret nonce, potentially negated)
    /// - e = challenge hash
    /// - a = KeyAgg coefficient for our key
    /// - d = private key (potentially negated based on aggregated key parity)
    /// 
    /// Nonce negation: If R has odd Y, negate k
    /// Key negation: If Q has odd Y, negate d
    /// </summary>
    public byte[] SignPartial()
    {
        if (_ourSecNonce1 == null || _ourSecNonce2 == null || _sessionHash == null || _aggregatedNonce == null)
        {
            throw new InvalidOperationException("Session not properly initialized");
        }

        // Compute the challenge: e = H("BIP0340/challenge" || R || P || m)
        var challenge = ComputeChallenge(_aggregatedNonce, _aggregatedPubKey, _sessionHash);

        // Compute binding factor b
        var b = _aggR1 != null && _aggR2 != null 
            ? ComputeNonceBindingFactor(_aggR1, _aggR2, _aggregatedPubKey, _sessionHash)
            : new Scalar(1);

        // Get secret nonce scalars
        var k1 = new Scalar(_ourSecNonce1, out _);
        var k2 = new Scalar(_ourSecNonce2, out _);
        
        // Compute effective nonce: k = k1 + b*k2
        var k = k1.Add(b.Multiply(k2));
        
        // BIP-327: Negate k if the aggregated nonce R has odd Y coordinate
        var rBytes = _aggregatedNonce.ToBytes();
        bool rHasOddY = rBytes[0] == 0x03;
        if (rHasOddY)
        {
            k = k.Negate();
            _logger.LogDebug("Negated nonce k due to odd Y in R");
        }
        
        // Get private key scalar
        var xBytes = new byte[32];
        _privateKey.WriteToSpan(xBytes);
        var d = new Scalar(xBytes, out _);
        
        // BIP-327: Negate d if the aggregated public key Q has odd Y coordinate
        var qBytes = _aggregatedPubKey.ToBytes();
        bool qHasOddY = qBytes[0] == 0x03;
        if (qHasOddY)
        {
            d = d.Negate();
            _logger.LogDebug("Negated private key d due to odd Y in Q");
        }
        
        // Compute KeyAgg coefficient for our key using the SAME order as was used in key aggregation
        var ourKeyBytes = _ourPublicKey.ToBytes();
        
        // Use the stored L hash and key order from aggregation
        // GetSecondKey: first key in _orderedCompressedKeys that differs from first key
        byte[]? pk2 = null;
        if (_orderedCompressedKeys.Count >= 1)
        {
            var firstKey = _orderedCompressedKeys[0];
            for (int i = 1; i < _orderedCompressedKeys.Count; i++)
            {
                if (CompareBytes(_orderedCompressedKeys[i], firstKey) != 0)
                {
                    pk2 = _orderedCompressedKeys[i];
                    break;
                }
            }
        }
        
        // Determine if we're the second key (coefficient = 1) or need hash
        Scalar a;
        if (pk2 != null && CompareBytes(ourKeyBytes, pk2) == 0)
        {
            a = new Scalar(1);
            _logger.LogDebug("Our key is pk2, using coefficient 1");
        }
        else
        {
            a = ComputeKeyAggCoeff(_keyAggLHash, ourKeyBytes);
            _logger.LogDebug("Using computed coefficient for our key");
        }

        // Compute partial signature: s = k + e*a*d (mod n)
        var e = new Scalar(challenge, out _);
        var partialSig = k.Add(e.Multiply(a).Multiply(d));

        var result = new byte[32];
        partialSig.WriteToSpan(result);
        
        _logger.LogDebug("SignPartial details - R odd Y: {ROdd}, Q odd Y: {QOdd}, pk2 match: {Pk2Match}", 
            rHasOddY, qHasOddY, pk2 != null && CompareBytes(ourKeyBytes, pk2) == 0);
        _logger.LogDebug("Created partial signature: {PartialSig}", Convert.ToHexString(result));

        return result;
    }

    /// <summary>
    /// Aggregate partial signatures into final signature
    /// </summary>
    public byte[] AggregatePartials(byte[] boltzPartialSig, byte[] ourPartialSig)
    {
        if (_aggregatedNonce == null)
        {
            throw new InvalidOperationException("Nonces not aggregated");
        }

        // Final signature = (R, s1 + s2)
        var s1 = new Scalar(boltzPartialSig, out _);
        var s2 = new Scalar(ourPartialSig, out _);
        var s = s1.Add(s2);

        // Build the 64-byte Schnorr signature: R (32 bytes) || s (32 bytes)
        var signature = new byte[64];
        var rBytes = _aggregatedNonce.ToBytes();
        Array.Copy(rBytes, 1, signature, 0, 32); // x-only R (skip prefix)
        s.WriteToSpan(signature.AsSpan(32));

        _logger.LogDebug("Aggregated signature: {Signature}", Convert.ToHexString(signature));

        return signature;
    }

    #region Private Helpers

    /// <summary>
    /// Aggregate public keys using BIP-327 MuSig2 KeyAgg algorithm.
    /// Keys are sorted by compressed representation for deterministic ordering
    /// so both parties agree on the aggregate key.
    /// Returns the aggregated key, the ordered keys, and the L hash.
    /// </summary>
    private static (ECPubKey aggregatedKey, List<byte[]> orderedKeys, byte[] lHash) AggregatePublicKeysWithOrder(ECPubKey pk1, ECPubKey pk2)
    {
        // Get 33-byte compressed representation (includes correct 02/03 prefix)
        var compressed1 = pk1.ToBytes();
        var compressed2 = pk2.ToBytes();

        // Sort keys for deterministic ordering (both parties must agree)
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

        // Compute L hash
        var lHash = ComputeKeyListHash(compressedKeys);

        // Aggregate
        var aggregated = KeyAggInternal(compressedKeys, ecPubKeys);

        return (aggregated, compressedKeys, lHash);
    }

    /// <summary>
    /// Aggregate public keys using BIP-327 MuSig2 KeyAgg algorithm.
    /// Keys are sorted by compressed representation for deterministic ordering
    /// so both parties agree on the aggregate key.
    /// </summary>
    private static ECPubKey AggregatePublicKeys(ECPubKey pk1, ECPubKey pk2)
    {
        // Get 33-byte compressed representation (includes correct 02/03 prefix)
        var compressed1 = pk1.ToBytes();
        var compressed2 = pk2.ToBytes();

        // Sort keys for deterministic ordering (both parties must agree)
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

        return KeyAggInternal(compressedKeys, ecPubKeys);
    }

    /// <summary>
    /// Get x-only (32-byte) representation of a public key
    /// </summary>
    private static byte[] GetXOnlyBytes(ECPubKey pk)
    {
        var full = pk.ToBytes();
        var xOnly = new byte[32];
        Array.Copy(full, 1, xOnly, 0, 32);
        return xOnly;
    }

    /// <summary>
    /// Compare two byte arrays lexicographically
    /// </summary>
    private static int CompareBytes(byte[] a, byte[] b)
    {
        for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            if (a[i] != b[i])
                return a[i].CompareTo(b[i]);
        }
        return a.Length.CompareTo(b.Length);
    }

    /// <summary>
    /// Multiply a public key by a scalar (point multiplication)
    /// </summary>
    private static ECPubKey MultiplyPubKey(ECPubKey pk, Scalar scalar)
    {
        var ge = pk.Q;
        var resultGej = ge.MultConst(scalar, 256);
        var resultGe = resultGej.ToGroupElement();
        return new ECPubKey(resultGe, Context.Instance);
    }

    /// <summary>
    /// Add two public keys (EC point addition)
    /// </summary>
    private static ECPubKey AddPubKeys(ECPubKey pk1, ECPubKey pk2)
    {
        var gej1 = pk1.Q.ToGroupElementJacobian();
        var gej2 = pk2.Q.ToGroupElementJacobian();

        var sum = gej1.AddVariable(gej2.ToGroupElement());
        var sumGe = sum.ToGroupElement();

        return new ECPubKey(sumGe, Context.Instance);
    }

    /// <summary>
    /// Compute the Schnorr signature challenge
    /// </summary>
    private static byte[] ComputeChallenge(ECPubKey nonce, ECPubKey pubkey, byte[] message)
    {
        // e = H_BIP340(R || P || m)
        var rBytes = nonce.ToBytes();
        var pBytes = pubkey.ToBytes();

        // Tagged hash: SHA256(SHA256("BIP0340/challenge") || SHA256("BIP0340/challenge") || data)
        var tag = CryptoSHA256.HashData(System.Text.Encoding.UTF8.GetBytes("BIP0340/challenge"));

        var data = new byte[tag.Length * 2 + 32 + 32 + message.Length];
        var offset = 0;
        Array.Copy(tag, 0, data, offset, tag.Length); offset += tag.Length;
        Array.Copy(tag, 0, data, offset, tag.Length); offset += tag.Length;
        Array.Copy(rBytes, 1, data, offset, 32); offset += 32; // x-only R
        Array.Copy(pBytes, 1, data, offset, 32); offset += 32; // x-only P
        Array.Copy(message, 0, data, offset, message.Length);

        return CryptoSHA256.HashData(data);
    }

    /// <summary>
    /// Compute partial signature s = k + e*x (mod n)
    /// </summary>
    private static byte[] ComputePartialSignature(ECPrivKey nonceKey, ECPrivKey privateKey, byte[] challenge)
    {
        var kBytes = new byte[32];
        nonceKey.WriteToSpan(kBytes);
        var k = new Scalar(kBytes, out _);

        var xBytes = new byte[32];
        privateKey.WriteToSpan(xBytes);
        var x = new Scalar(xBytes, out _);

        var e = new Scalar(challenge, out _);

        var s = k.Add(e.Multiply(x));

        var result = new byte[32];
        s.WriteToSpan(result);
        return result;
    }

    #endregion
}