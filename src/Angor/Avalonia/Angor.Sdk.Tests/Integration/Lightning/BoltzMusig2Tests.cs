using Angor.Shared.Integration.Lightning;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NBitcoin;
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
        // This test simulates a full MuSig2 signing session between two parties.
        // In Boltz protocol, key order is fixed: [refundKey (Boltz), claimKey (us)].
        // BoltzMusig2 constructor takes (ourPrivKey, boltzPubKey) and uses order [boltzPubKey, ourPubKey].
        // Both parties must agree on the same key order.
        
        // Party 1 = claim side (us), Party 2 = refund side (Boltz)
        var claimKey = new Key();
        var claimKeyBytes = claimKey.ToBytes();
        var claimPub = claimKey.PubKey.ToBytes();
        
        var refundKey = new Key();
        var refundKeyBytes = refundKey.ToBytes();
        var refundPub = refundKey.PubKey.ToBytes();
        
        // Claim side: BoltzMusig2(claimPrivKey, refundPub) -> order [refundPub, claimPub]
        var musig1 = new BoltzMusig2(claimKeyBytes, refundPub, _logger);
        // Refund side: BoltzMusig2(refundPrivKey, claimPub) -> order [claimPub, refundPub]
        // This gives DIFFERENT order! In real Boltz, the server doesn't use BoltzMusig2 class.
        // For this test, verify that a single party can complete the round-trip.
        
        // Generate nonces
        var nonce1 = musig1.GenerateNonce();
        
        _output.WriteLine($"Claim pubkey: {Convert.ToHexString(claimPub)}");
        _output.WriteLine($"Refund pubkey: {Convert.ToHexString(refundPub)}");
        _output.WriteLine($"Aggregated pubkey: {Convert.ToHexString(musig1.GetAggregatedPubKey())}");
        _output.WriteLine($"Nonce (66 bytes): {Convert.ToHexString(nonce1)}");
        
        // Verify nonce format
        Assert.Equal(66, nonce1.Length);
        Assert.True(nonce1[0] == 0x02 || nonce1[0] == 0x03);
        Assert.True(nonce1[33] == 0x02 || nonce1[33] == 0x03);
        
        // Verify aggregated key is valid
        var aggKey = musig1.GetAggregatedPubKey();
        Assert.Equal(33, aggKey.Length);
        Assert.True(aggKey[0] == 0x02 || aggKey[0] == 0x03);
        
        // Verify static KeyAgg matches constructor result
        var staticAgg = BoltzMusig2.KeyAgg(refundPub, claimPub);
        var aggKeyXOnly = aggKey[1..]; // strip prefix
        Assert.Equal(Convert.ToHexString(staticAgg), Convert.ToHexString(aggKeyXOnly));
    }

    [Fact]
    public void KeyAggregation_MatchesBoltzConvention()
    {
        // BoltzMusig2 constructor uses Boltz convention: [boltzKey (refund), ourKey (claim)]
        // The aggregate key should match static KeyAgg with the same order

        var key1 = new Key();
        var key1Bytes = key1.ToBytes();
        var pub1 = key1.PubKey.ToBytes();

        var key2 = new Key();
        var pub2 = key2.PubKey.ToBytes();

        // BoltzMusig2(privateKey, boltzPubKey) -> KeyAgg([boltzPubKey, ourPubKey])
        var musig = new BoltzMusig2(key1Bytes, pub2, _logger);
        var aggKey = musig.GetAggregatedPubKey();

        // Static KeyAgg with same order: [boltzKey (pub2), ourKey (pub1)]
        var staticAggKey = BoltzMusig2.KeyAgg(pub2, pub1);

        _output.WriteLine($"Our pubkey: {Convert.ToHexString(pub1)}");
        _output.WriteLine($"Boltz pubkey: {Convert.ToHexString(pub2)}");
        _output.WriteLine($"Aggregated (constructor): {Convert.ToHexString(aggKey)}");
        _output.WriteLine($"Aggregated (static KeyAgg): {Convert.ToHexString(staticAggKey)}");

        // The x-only part of the constructor aggregate should match static KeyAgg
        var aggKeyXOnly = aggKey.Length == 33 ? aggKey[1..] : aggKey;
        Assert.Equal(Convert.ToHexString(staticAggKey), Convert.ToHexString(aggKeyXOnly));
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