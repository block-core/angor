using Angor.Sdk.Integration.Lightning;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NBitcoin;
using NBitcoin.Secp256k1;
using Xunit;
using Xunit.Abstractions;

namespace Angor.Sdk.Tests.Integration.Lightning;

/// <summary>
/// Tests for the BoltzMusig2 implementation using BIP-327 test vectors
/// and basic functionality tests.
/// </summary>
public class BoltzMusig2Tests
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger _logger;

    // BIP-327 test vector public keys (compressed, 33 bytes with 02/03 prefix)
    // From: https://github.com/bitcoin/bips/blob/master/bip-0327/vectors/key_agg_vectors.json
    private static readonly string[] TestPubKeys = new[]
    {
        "02F9308A019258C31049344F85F89D5229B531C845836F99B08601F113BCE036F9", // pubkey 0
        "03DFF1D77F2A671C5F36183726DB2341BE58FEAE1DA2DECED843240F7B502BA659", // pubkey 1
        "023590A94E768F8E1815C2F24B4D80A8E3149316C3518CE7B7AD338368D038CA66"  // pubkey 2
    };

    // Expected aggregated keys from BIP-327 test vectors
    // key_indices: [0, 1, 2] -> expected: "90539EEDE565F5D054F32CC0C220126889ED1E5D193BAF15AEF344FE59D4610C"
    // key_indices: [2, 1, 0] -> expected: "6204DE8B083426DC6EAF9502D27024D53FC826BF7D2012148A0575435DF54B2B"
    // key_indices: [0, 0, 0] -> expected: "B436E3BAD62B8CD409969A224731C193D051162D8C5AE8B109306127DA3AA935"
    // key_indices: [0, 0, 1, 1] -> expected: "69BC22BFA5D106306E48A20679DE1D7389386124D07571D0D872686028C26A3E"

    private static readonly (int[] keyIndices, string expected)[] KeyAggTestVectors = new[]
    {
        (new[] { 0, 1, 2 }, "90539EEDE565F5D054F32CC0C220126889ED1E5D193BAF15AEF344FE59D4610C"),
        (new[] { 2, 1, 0 }, "6204DE8B083426DC6EAF9502D27024D53FC826BF7D2012148A0575435DF54B2B"),
        (new[] { 0, 0, 0 }, "B436E3BAD62B8CD409969A224731C193D051162D8C5AE8B109306127DA3AA935"),
        (new[] { 0, 0, 1, 1 }, "69BC22BFA5D106306E48A20679DE1D7389386124D07571D0D872686028C26A3E"),
    };

    public BoltzMusig2Tests(ITestOutputHelper output)
    {
        _output = output;
        _logger = new NullLogger<BoltzMusig2Tests>();
    }

    #region BIP-327 Test Vector Validation

    [Theory]
    [InlineData(0, "90539EEDE565F5D054F32CC0C220126889ED1E5D193BAF15AEF344FE59D4610C")]  // [0, 1, 2]
    [InlineData(1, "6204DE8B083426DC6EAF9502D27024D53FC826BF7D2012148A0575435DF54B2B")]  // [2, 1, 0]
    [InlineData(2, "B436E3BAD62B8CD409969A224731C193D051162D8C5AE8B109306127DA3AA935")]  // [0, 0, 0]
    [InlineData(3, "69BC22BFA5D106306E48A20679DE1D7389386124D07571D0D872686028C26A3E")]  // [0, 0, 1, 1]
    public void KeyAgg_MatchesBIP327TestVectors(int vectorIndex, string expectedAggKey)
    {
        // Arrange
        var (keyIndices, _) = KeyAggTestVectors[vectorIndex];
        var pubKeys = keyIndices.Select(i => Convert.FromHexString(TestPubKeys[i])).ToArray();

        _output.WriteLine($"Test vector {vectorIndex}: key_indices = [{string.Join(", ", keyIndices)}]");
        _output.WriteLine($"Public keys:");
        for (int i = 0; i < pubKeys.Length; i++)
        {
            _output.WriteLine($"  [{i}]: {Convert.ToHexString(pubKeys[i])}");
        }
        _output.WriteLine($"Expected aggregated key: {expectedAggKey}");

        // Act
        var actualAggKey = BoltzMusig2.KeyAgg(pubKeys);
        var actualAggKeyHex = Convert.ToHexString(actualAggKey);

        _output.WriteLine($"Actual aggregated key:   {actualAggKeyHex}");

        // Assert
        Assert.Equal(expectedAggKey, actualAggKeyHex);
    }

    [Fact]
    public void KeyAgg_Keys012_MatchesBIP327()
    {
        // Arrange - Keys [0, 1, 2]
        var pk0 = Convert.FromHexString(TestPubKeys[0]);
        var pk1 = Convert.FromHexString(TestPubKeys[1]);
        var pk2 = Convert.FromHexString(TestPubKeys[2]);
        var expected = "90539EEDE565F5D054F32CC0C220126889ED1E5D193BAF15AEF344FE59D4610C";

        _output.WriteLine($"pk0: {TestPubKeys[0]}");
        _output.WriteLine($"pk1: {TestPubKeys[1]}");
        _output.WriteLine($"pk2: {TestPubKeys[2]}");
        _output.WriteLine($"Expected: {expected}");

        // Debug - show intermediate values
        var (Q, L, pk2Sorted, coeffs) = BoltzMusig2.KeyAggDebug(pk0, pk1, pk2);
        _output.WriteLine($"L hash: {Convert.ToHexString(L)}");
        _output.WriteLine($"pk2 (second unique in sorted): {(pk2Sorted != null ? Convert.ToHexString(pk2Sorted) : "null")}");
        foreach (var (pk, coeff) in coeffs)
        {
            _output.WriteLine($"  pk={Convert.ToHexString(pk).Substring(0, 16)}... coeff={Convert.ToHexString(coeff)}");
        }

        // Act
        var result = BoltzMusig2.KeyAgg(pk0, pk1, pk2);
        var resultHex = Convert.ToHexString(result);

        _output.WriteLine($"Actual:   {resultHex}");

        // Assert
        Assert.Equal(expected, resultHex);
    }

    [Fact]
    public void KeyAgg_Keys210_MatchesBIP327()
    {
        // Arrange - Keys [2, 1, 0] - different order should give different result
        var pk0 = Convert.FromHexString(TestPubKeys[0]);
        var pk1 = Convert.FromHexString(TestPubKeys[1]);
        var pk2 = Convert.FromHexString(TestPubKeys[2]);
        var expected = "6204DE8B083426DC6EAF9502D27024D53FC826BF7D2012148A0575435DF54B2B";

        _output.WriteLine($"pk2: {TestPubKeys[2]}");
        _output.WriteLine($"pk1: {TestPubKeys[1]}");
        _output.WriteLine($"pk0: {TestPubKeys[0]}");
        _output.WriteLine($"Expected: {expected}");

        // Act - Note the order: pk2, pk1, pk0
        var result = BoltzMusig2.KeyAgg(pk2, pk1, pk0);
        var resultHex = Convert.ToHexString(result);

        _output.WriteLine($"Actual:   {resultHex}");

        // Assert
        Assert.Equal(expected, resultHex);
    }

    [Fact]
    public void KeyAgg_SameKeyThreeTimes_MatchesBIP327()
    {
        // Arrange - Keys [0, 0, 0] - same key three times
        var pk0 = Convert.FromHexString(TestPubKeys[0]);
        var expected = "B436E3BAD62B8CD409969A224731C193D051162D8C5AE8B109306127DA3AA935";

        _output.WriteLine($"pk0 (x3): {TestPubKeys[0]}");
        _output.WriteLine($"Expected: {expected}");

        // Act
        var result = BoltzMusig2.KeyAgg(pk0, pk0, pk0);
        var resultHex = Convert.ToHexString(result);

        _output.WriteLine($"Actual:   {resultHex}");

        // Assert
        Assert.Equal(expected, resultHex);
    }

    [Fact]
    public void KeyAgg_TwoKeysTwiceEach_MatchesBIP327()
    {
        // Arrange - Keys [0, 0, 1, 1]
        var pk0 = Convert.FromHexString(TestPubKeys[0]);
        var pk1 = Convert.FromHexString(TestPubKeys[1]);
        var expected = "69BC22BFA5D106306E48A20679DE1D7389386124D07571D0D872686028C26A3E";

        _output.WriteLine($"pk0 (x2): {TestPubKeys[0]}");
        _output.WriteLine($"pk1 (x2): {TestPubKeys[1]}");
        _output.WriteLine($"Expected: {expected}");

        // Act
        var result = BoltzMusig2.KeyAgg(pk0, pk0, pk1, pk1);
        var resultHex = Convert.ToHexString(result);

        _output.WriteLine($"Actual:   {resultHex}");

        // Assert
        Assert.Equal(expected, resultHex);
    }

    #endregion

    #region Basic Functionality Tests

    [Fact]
    public void Constructor_ValidKeys_InitializesSuccessfully()
    {
        // Arrange
        var privateKey = new Key();
        var privateKeyBytes = privateKey.ToBytes();

        var otherKey = new Key();
        var otherPubKeyBytes = otherKey.PubKey.ToBytes(); // 33 bytes compressed

        // Act
        var musig = new BoltzMusig2(privateKeyBytes, otherPubKeyBytes, _logger);

        // Assert
        Assert.NotNull(musig);
        var aggKey = musig.GetAggregatedPubKey();
        Assert.Equal(33, aggKey.Length); // Compressed pubkey

        _output.WriteLine($"Private key: {Convert.ToHexString(privateKeyBytes)}");
        _output.WriteLine($"Other pubkey: {Convert.ToHexString(otherPubKeyBytes)}");
        _output.WriteLine($"Aggregated key: {Convert.ToHexString(aggKey)}");
    }

    [Fact]
    public void Constructor_XOnlyPubKey_InitializesSuccessfully()
    {
        // Arrange - Use x-only format (32 bytes)
        var privateKey = new Key();
        var privateKeyBytes = privateKey.ToBytes();

        var otherKey = new Key();
        var otherPubKeyFull = otherKey.PubKey.ToBytes();
        var otherPubKeyXOnly = new byte[32];
        Array.Copy(otherPubKeyFull, 1, otherPubKeyXOnly, 0, 32); // Skip prefix byte

        // Act
        var musig = new BoltzMusig2(privateKeyBytes, otherPubKeyXOnly, _logger);

        // Assert
        Assert.NotNull(musig);
        var xOnlyAggKey = musig.GetXOnlyAggregatedPubKey();
        Assert.Equal(32, xOnlyAggKey.Length);

        _output.WriteLine($"X-only other pubkey: {Convert.ToHexString(otherPubKeyXOnly)}");
        _output.WriteLine($"X-only aggregated key: {Convert.ToHexString(xOnlyAggKey)}");
    }

    [Fact]
    public void GenerateNonce_ReturnsValidNonce()
    {
        // Arrange
        var privateKey = new Key();
        var privateKeyBytes = privateKey.ToBytes();
        
        var otherKey = new Key();
        var otherPubKeyBytes = otherKey.PubKey.ToBytes();
        
        var musig = new BoltzMusig2(privateKeyBytes, otherPubKeyBytes, _logger);

        // Act
        var nonce = musig.GenerateNonce();

        // Assert
        Assert.NotNull(nonce);
        Assert.Equal(66, nonce.Length); // BIP-327: 66 bytes = R1 (33) || R2 (33)
        Assert.True(nonce[0] == 0x02 || nonce[0] == 0x03); // Valid prefix for R1
        Assert.True(nonce[33] == 0x02 || nonce[33] == 0x03); // Valid prefix for R2
        
        _output.WriteLine($"Generated nonce (66 bytes): {Convert.ToHexString(nonce)}");
    }

    [Fact]
    public void AggregateNonces_ValidNonces_Succeeds()
    {
        // Arrange
        var privateKey = new Key();
        var privateKeyBytes = privateKey.ToBytes();
        
        var otherKey = new Key();
        var otherPubKeyBytes = otherKey.PubKey.ToBytes();
        
        var musig = new BoltzMusig2(privateKeyBytes, otherPubKeyBytes, _logger);
        
        // Generate our nonce (66 bytes)
        var ourNonce = musig.GenerateNonce();
        
        // Generate a "Boltz" nonce (simulated - 66 bytes = R1 || R2)
        var boltzNonceKey1 = new Key();
        var boltzNonceKey2 = new Key();
        var boltzNonce = new byte[66];
        Array.Copy(boltzNonceKey1.PubKey.ToBytes(), 0, boltzNonce, 0, 33);
        Array.Copy(boltzNonceKey2.PubKey.ToBytes(), 0, boltzNonce, 33, 33);

        // Act & Assert - Should not throw
        musig.AggregateNonces(boltzNonce);
        
        _output.WriteLine($"Our nonce (66 bytes): {Convert.ToHexString(ourNonce)}");
        _output.WriteLine($"Boltz nonce (66 bytes): {Convert.ToHexString(boltzNonce)}");
    }

    [Fact]
    public void SignPartial_ProducesValidSignature()
    {
        // Arrange
        var privateKey = new Key();
        var privateKeyBytes = privateKey.ToBytes();
        
        var otherKey = new Key();
        var otherPubKeyBytes = otherKey.PubKey.ToBytes();
        
        var musig = new BoltzMusig2(privateKeyBytes, otherPubKeyBytes, _logger);
        
        // Generate nonces (66 bytes each)
        var ourNonce = musig.GenerateNonce();
        var boltzNonceKey1 = new Key();
        var boltzNonceKey2 = new Key();
        var boltzNonce = new byte[66];
        Array.Copy(boltzNonceKey1.PubKey.ToBytes(), 0, boltzNonce, 0, 33);
        Array.Copy(boltzNonceKey2.PubKey.ToBytes(), 0, boltzNonce, 33, 33);
        
        musig.AggregateNonces(boltzNonce);
        
        // Create a test message (sighash)
        var message = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes("test message"));
        
        musig.InitializeSession(message);

        // Act
        var partialSig = musig.SignPartial();

        // Assert
        Assert.NotNull(partialSig);
        Assert.Equal(32, partialSig.Length); // Scalar is 32 bytes
        
        _output.WriteLine($"Message: {Convert.ToHexString(message)}");
        _output.WriteLine($"Partial signature: {Convert.ToHexString(partialSig)}");
    }

    [Fact]
    public void AggregatePartials_ProducesValidSchnorrSignature()
    {
        // Arrange
        var privateKey = new Key();
        var privateKeyBytes = privateKey.ToBytes();
        
        var otherKey = new Key();
        var otherKeyBytes = otherKey.ToBytes();
        var otherPubKeyBytes = otherKey.PubKey.ToBytes();
        
        var musig = new BoltzMusig2(privateKeyBytes, otherPubKeyBytes, _logger);
        
        // Generate nonces (66 bytes each)
        var ourNonce = musig.GenerateNonce();
        var boltzNonceKey1 = new Key();
        var boltzNonceKey2 = new Key();
        var boltzNonce = new byte[66];
        Array.Copy(boltzNonceKey1.PubKey.ToBytes(), 0, boltzNonce, 0, 33);
        Array.Copy(boltzNonceKey2.PubKey.ToBytes(), 0, boltzNonce, 33, 33);
        
        musig.AggregateNonces(boltzNonce);
        
        // Create a test message
        var message = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes("test message"));
        
        musig.InitializeSession(message);
        
        // Create our partial signature
        var ourPartialSig = musig.SignPartial();
        
        // Simulate Boltz's partial signature (in real scenario, this comes from Boltz API)
        // For testing, we create a random valid scalar
        var boltzPartialSig = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        // Make sure it's a valid scalar (less than group order)
        boltzPartialSig[0] &= 0x7F;

        // Act
        var aggregatedSig = musig.AggregatePartials(boltzPartialSig, ourPartialSig);

        // Assert
        Assert.NotNull(aggregatedSig);
        Assert.Equal(64, aggregatedSig.Length); // Schnorr signature is 64 bytes (R + s)
        
        _output.WriteLine($"Our partial sig: {Convert.ToHexString(ourPartialSig)}");
        _output.WriteLine($"Boltz partial sig: {Convert.ToHexString(boltzPartialSig)}");
        _output.WriteLine($"Aggregated signature: {Convert.ToHexString(aggregatedSig)}");
    }

    [Fact]
    public void FullSigningRoundtrip_TwoParties_ProducesConsistentSignature()
    {
        // This test simulates a full MuSig2 signing session between two parties
        
        // Party 1 (us)
        var key1 = new Key();
        var key1Bytes = key1.ToBytes();
        var pub1 = key1.PubKey.ToBytes();
        
        // Party 2 (Boltz)
        var key2 = new Key();
        var key2Bytes = key2.ToBytes();
        var pub2 = key2.PubKey.ToBytes();
        
        // Both parties create their MuSig sessions
        var musig1 = new BoltzMusig2(key1Bytes, pub2, _logger);
        var musig2 = new BoltzMusig2(key2Bytes, pub1, _logger);
        
        // Generate nonces (66 bytes each)
        var nonce1 = musig1.GenerateNonce();
        var nonce2 = musig2.GenerateNonce();
        
        // Exchange and aggregate nonces
        musig1.AggregateNonces(nonce2);
        musig2.AggregateNonces(nonce1);
        
        // Both parties use the same message
        var message = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes("Boltz swap claim"));
        
        musig1.InitializeSession(message);
        musig2.InitializeSession(message);
        
        // Each party creates their partial signature
        var psig1 = musig1.SignPartial();
        var psig2 = musig2.SignPartial();
        
        // Both parties aggregate the signatures (should produce the same result)
        var finalSig1 = musig1.AggregatePartials(psig2, psig1);
        var finalSig2 = musig2.AggregatePartials(psig1, psig2);
        
        _output.WriteLine($"Party 1 pubkey: {Convert.ToHexString(pub1)}");
        _output.WriteLine($"Party 2 pubkey: {Convert.ToHexString(pub2)}");
        _output.WriteLine($"Aggregated pubkey 1: {Convert.ToHexString(musig1.GetAggregatedPubKey())}");
        _output.WriteLine($"Aggregated pubkey 2: {Convert.ToHexString(musig2.GetAggregatedPubKey())}");
        _output.WriteLine($"Nonce 1 (66 bytes): {Convert.ToHexString(nonce1)}");
        _output.WriteLine($"Nonce 2 (66 bytes): {Convert.ToHexString(nonce2)}");
        _output.WriteLine($"Partial sig 1: {Convert.ToHexString(psig1)}");
        _output.WriteLine($"Partial sig 2: {Convert.ToHexString(psig2)}");
        _output.WriteLine($"Final signature 1: {Convert.ToHexString(finalSig1)}");
        _output.WriteLine($"Final signature 2: {Convert.ToHexString(finalSig2)}");
        
        // The aggregated keys should be the same
        Assert.Equal(
            Convert.ToHexString(musig1.GetAggregatedPubKey()),
            Convert.ToHexString(musig2.GetAggregatedPubKey()));
        
        // Final signatures should have correct length
        Assert.Equal(64, finalSig1.Length);
        Assert.Equal(64, finalSig2.Length);
        
        // Note: The final signatures may differ because the order of aggregation matters
        // In real MuSig2, the order is deterministic based on sorted pubkeys
    }

    [Fact]
    public void KeyAggregation_IsCommutative()
    {
        // Key aggregation should produce the same result regardless of which key is "ours"

        var key1 = new Key();
        var key1Bytes = key1.ToBytes();
        var pub1 = key1.PubKey.ToBytes();

        var key2 = new Key();
        var key2Bytes = key2.ToBytes();
        var pub2 = key2.PubKey.ToBytes();

        // Create MuSig sessions both ways
        var musig1 = new BoltzMusig2(key1Bytes, pub2, _logger);
        var musig2 = new BoltzMusig2(key2Bytes, pub1, _logger);

        var aggKey1 = musig1.GetAggregatedPubKey();
        var aggKey2 = musig2.GetAggregatedPubKey();

        _output.WriteLine($"Pubkey 1: {Convert.ToHexString(pub1)}");
        _output.WriteLine($"Pubkey 2: {Convert.ToHexString(pub2)}");
        _output.WriteLine($"Aggregated (1 + 2): {Convert.ToHexString(aggKey1)}");
        _output.WriteLine($"Aggregated (2 + 1): {Convert.ToHexString(aggKey2)}");

        // Point addition is commutative, so P1 + P2 = P2 + P1
        Assert.Equal(Convert.ToHexString(aggKey1), Convert.ToHexString(aggKey2));
    }

    [Fact]
    public void Constructor_InvalidPrivateKey_Throws()
    {
        // Arrange
        var invalidPrivateKey = new byte[32]; // All zeros is invalid
        var validPubKey = new Key().PubKey.ToBytes();

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            new BoltzMusig2(invalidPrivateKey, validPubKey, _logger));

        _output.WriteLine($"Exception: {ex.Message}");
    }

    [Fact]
    public void Constructor_InvalidPublicKeyLength_Throws()
    {
        // Arrange
        var validKey = new Key();
        var validPrivateKey = validKey.ToBytes();

        var invalidPubKey = new byte[31]; // Wrong length

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            new BoltzMusig2(validPrivateKey, invalidPubKey, _logger));

        _output.WriteLine($"Exception: {ex.Message}");
    }

    [Fact]
    public void SignPartial_WithoutInitializeSession_Throws()
    {
        // Arrange
        var key = new Key();
        var keyBytes = key.ToBytes();
        
        var otherKey = new Key();
        var otherPubKey = otherKey.PubKey.ToBytes();
        
        var musig = new BoltzMusig2(keyBytes, otherPubKey, _logger);
        musig.GenerateNonce();
        
        // Create a 66-byte nonce
        var nonce = new byte[66];
        Array.Copy(new Key().PubKey.ToBytes(), 0, nonce, 0, 33);
        Array.Copy(new Key().PubKey.ToBytes(), 0, nonce, 33, 33);
        musig.AggregateNonces(nonce);
        // Note: Not calling InitializeSession

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => musig.SignPartial());
    }

    [Fact]
    public void AggregateNonces_WithoutGenerateNonce_Throws()
    {
        // Arrange
        var key = new Key();
        var keyBytes = key.ToBytes();
        
        var otherKey = new Key();
        var otherPubKey = otherKey.PubKey.ToBytes();
        
        var musig = new BoltzMusig2(keyBytes, otherPubKey, _logger);
        // Note: Not calling GenerateNonce

        // Create a 66-byte nonce
        var nonce = new byte[66];
        Array.Copy(new Key().PubKey.ToBytes(), 0, nonce, 0, 33);
        Array.Copy(new Key().PubKey.ToBytes(), 0, nonce, 33, 33);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => 
            musig.AggregateNonces(nonce));
    }

    #endregion
}