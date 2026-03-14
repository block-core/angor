using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.BIP32;
using Blockcore.NBitcoin.BIP39;
using Blockcore.Networks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Angor.Test;

/// <summary>
/// Tests for descriptor-backed standard wallet address derivation (Phase 1 – wpkh).
///
/// Verifies:
///   1. Descriptor format is correct.
///   2. XPub can be round-tripped (extract → use for derivation).
///   3. Descriptor-derived addresses are identical to the legacy derivation.
///   4. Old AccountInfo records (no descriptors) are migrated automatically.
///   5. After migration, subsequent scans produce the same addresses.
/// </summary>
public class WalletDescriptorTests
{
    // Fixed mnemonic so the test vectors are deterministic.
    private const string TestMnemonic =
        "sorry poet adapt sister barely loud praise spray option oxygen hero surround";

    private readonly Network _testnet;
    private readonly Mock<INetworkConfiguration> _networkConfig;
    private readonly WalletOperations _walletOps;
    private readonly HdOperations _hdOps;

    public WalletDescriptorTests()
    {
        _testnet = Networks.Bitcoin.Testnet();
        _networkConfig = new Mock<INetworkConfiguration>();
        _networkConfig.Setup(x => x.GetNetwork()).Returns(_testnet);

        _hdOps = new HdOperations();
        _walletOps = new WalletOperations(
            null!,           // IIndexerService – not needed for non-scan tests
            _hdOps,
            NullLogger<WalletOperations>.Instance,
            _networkConfig.Object);
    }

    // ── Descriptor building ──────────────────────────────────────────────────

    [Fact]
    public void Build_WithFingerprint_ProducesCorrectFormat()
    {
        const string xpub = "tpubDEFABC";
        const string fp = "aabbccdd";
        var (recv, chng) = StandardWalletDescriptors.Build(xpub, fp, coinType: 1);

        Assert.Equal($"wpkh([aabbccdd/84h/1h/0h]{xpub}/0/*)", recv);
        Assert.Equal($"wpkh([aabbccdd/84h/1h/0h]{xpub}/1/*)", chng);
    }

    [Fact]
    public void Build_WithoutFingerprint_OmitsOriginBlock()
    {
        const string xpub = "tpubDEFABC";
        var (recv, chng) = StandardWalletDescriptors.Build(xpub, null, coinType: 1);

        Assert.Equal($"wpkh({xpub}/0/*)", recv);
        Assert.Equal($"wpkh({xpub}/1/*)", chng);
    }

    // ── XPub extraction ──────────────────────────────────────────────────────

    [Fact]
    public void ExtractXPub_WithOrigin_ReturnsXPub()
    {
        const string xpub = "tpubDEFABC";
        const string descriptor = $"wpkh([aabbccdd/84h/1h/0h]{xpub}/0/*)";
        Assert.Equal(xpub, StandardWalletDescriptors.ExtractXPub(descriptor));
    }

    [Fact]
    public void ExtractXPub_WithoutOrigin_ReturnsXPub()
    {
        const string xpub = "tpubDEFABC";
        const string descriptor = $"wpkh({xpub}/1/*)";
        Assert.Equal(xpub, StandardWalletDescriptors.ExtractXPub(descriptor));
    }

    [Fact]
    public void ExtractXPub_ChangeDescriptor_ReturnsSameXPub()
    {
        const string xpub = "tpubABCD1234";
        var (recv, chng) = StandardWalletDescriptors.Build(xpub, "deadbeef", coinType: 1);
        Assert.Equal(xpub, StandardWalletDescriptors.ExtractXPub(recv));
        Assert.Equal(xpub, StandardWalletDescriptors.ExtractXPub(chng));
    }

    // ── Branch detection ─────────────────────────────────────────────────────

    [Fact]
    public void IsChangeBranch_ReturnsFalseForReceiveDescriptor()
    {
        var (recv, _) = StandardWalletDescriptors.Build("tpub", null, 1);
        Assert.False(StandardWalletDescriptors.IsChangeBranch(recv));
    }

    [Fact]
    public void IsChangeBranch_ReturnsTrueForChangeDescriptor()
    {
        var (_, chng) = StandardWalletDescriptors.Build("tpub", null, 1);
        Assert.True(StandardWalletDescriptors.IsChangeBranch(chng));
    }

    // ── BuildAccountInfoForWalletWords populates descriptors ─────────────────

    [Fact]
    public void BuildAccountInfo_PopulatesDescriptorFields()
    {
        var words = new WalletWords { Words = TestMnemonic };
        var info = _walletOps.BuildAccountInfoForWalletWords(words);

        Assert.False(string.IsNullOrEmpty(info.ReceiveDescriptor),
            "ReceiveDescriptor must be set");
        Assert.False(string.IsNullOrEmpty(info.ChangeDescriptor),
            "ChangeDescriptor must be set");
        Assert.False(string.IsNullOrEmpty(info.MasterFingerprint),
            "MasterFingerprint must be set");
    }

    [Fact]
    public void BuildAccountInfo_ReceiveDescriptorContainsExtPubKey()
    {
        var words = new WalletWords { Words = TestMnemonic };
        var info = _walletOps.BuildAccountInfoForWalletWords(words);

        var xpubFromDescriptor = StandardWalletDescriptors.ExtractXPub(info.ReceiveDescriptor!);
        Assert.Equal(info.ExtPubKey, xpubFromDescriptor);
    }

    [Fact]
    public void BuildAccountInfo_ChangeDescriptorContainsExtPubKey()
    {
        var words = new WalletWords { Words = TestMnemonic };
        var info = _walletOps.BuildAccountInfoForWalletWords(words);

        var xpubFromDescriptor = StandardWalletDescriptors.ExtractXPub(info.ChangeDescriptor!);
        Assert.Equal(info.ExtPubKey, xpubFromDescriptor);
    }

    [Fact]
    public void BuildAccountInfo_ReceiveDescriptorIsReceiveBranch()
    {
        var words = new WalletWords { Words = TestMnemonic };
        var info = _walletOps.BuildAccountInfoForWalletWords(words);
        Assert.False(StandardWalletDescriptors.IsChangeBranch(info.ReceiveDescriptor!));
    }

    [Fact]
    public void BuildAccountInfo_ChangeDescriptorIsChangeBranch()
    {
        var words = new WalletWords { Words = TestMnemonic };
        var info = _walletOps.BuildAccountInfoForWalletWords(words);
        Assert.True(StandardWalletDescriptors.IsChangeBranch(info.ChangeDescriptor!));
    }

    // ── Address derivation compatibility ────────────────────────────────────

    /// <summary>
    /// Descriptor-derived addresses must match the legacy derivation for every index.
    /// </summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(4, false)]
    [InlineData(0, true)]
    [InlineData(3, true)]
    public void DescriptorDerivedAddress_MatchesLegacyAddress(int index, bool isChange)
    {
        var words = new WalletWords { Words = TestMnemonic };
        var info = _walletOps.BuildAccountInfoForWalletWords(words);

        // Legacy derivation: use ExtPubKey + isChange flag directly.
        var accountExtPubKey = ExtPubKey.Parse(info.ExtPubKey, _testnet);
        var legacyPubKey = _hdOps.GeneratePublicKey(accountExtPubKey, index, isChange);
        var legacyAddress = legacyPubKey.GetSegwitAddress(_testnet).ToString();

        // Descriptor-backed derivation: extract xpub from the descriptor.
        var descriptor = isChange ? info.ChangeDescriptor! : info.ReceiveDescriptor!;
        var xpubStr = StandardWalletDescriptors.ExtractXPub(descriptor);
        var descriptorExtPubKey = ExtPubKey.Parse(xpubStr, _testnet);
        var descriptorPubKey = _hdOps.GeneratePublicKey(descriptorExtPubKey, index, isChange);
        var descriptorAddress = descriptorPubKey.GetSegwitAddress(_testnet).ToString();

        Assert.Equal(legacyAddress, descriptorAddress);
    }

    // ── Migration of legacy AccountInfo ──────────────────────────────────────

    [Fact]
    public void TryMigrate_LegacyAccountInfo_PopulatesDescriptors()
    {
        var words = new WalletWords { Words = TestMnemonic };
        var fresh = _walletOps.BuildAccountInfoForWalletWords(words);

        // Simulate a legacy record: same xpub/path but no descriptors.
        var legacy = new AccountInfo
        {
            walletId = fresh.walletId,
            ExtPubKey = fresh.ExtPubKey,
            RootExtPubKey = fresh.RootExtPubKey,
            Path = fresh.Path
            // ReceiveDescriptor, ChangeDescriptor, MasterFingerprint are null
        };

        var migrated = StandardWalletDescriptors.TryMigrate(legacy, _testnet.Consensus.CoinType);

        Assert.True(migrated, "TryMigrate should return true when descriptors were generated");
        Assert.False(string.IsNullOrEmpty(legacy.ReceiveDescriptor));
        Assert.False(string.IsNullOrEmpty(legacy.ChangeDescriptor));
    }

    [Fact]
    public void TryMigrate_AlreadyMigrated_ReturnsFalse()
    {
        var words = new WalletWords { Words = TestMnemonic };
        var info = _walletOps.BuildAccountInfoForWalletWords(words);

        // info already has descriptors from BuildAccountInfoForWalletWords.
        var migrated = StandardWalletDescriptors.TryMigrate(info, _testnet.Consensus.CoinType);

        Assert.False(migrated, "TryMigrate should be a no-op when descriptors are already present");
    }

    [Fact]
    public void TryMigrate_LegacyAddressesMatchFreshAddresses()
    {
        var words = new WalletWords { Words = TestMnemonic };
        var fresh = _walletOps.BuildAccountInfoForWalletWords(words);

        // Simulate a legacy record.
        var legacy = new AccountInfo
        {
            walletId = fresh.walletId,
            ExtPubKey = fresh.ExtPubKey,
            RootExtPubKey = fresh.RootExtPubKey,
            Path = fresh.Path
        };

        StandardWalletDescriptors.TryMigrate(legacy, _testnet.Consensus.CoinType);

        // After migration, addresses derived from both should be identical.
        for (var i = 0; i < 5; i++)
        {
            var freshXPub = StandardWalletDescriptors.ExtractXPub(fresh.ReceiveDescriptor!);
            var legacyXPub = StandardWalletDescriptors.ExtractXPub(legacy.ReceiveDescriptor!);

            var freshExtPubKey = ExtPubKey.Parse(freshXPub, _testnet);
            var legacyExtPubKey = ExtPubKey.Parse(legacyXPub, _testnet);

            var freshPubKey = _hdOps.GeneratePublicKey(freshExtPubKey, i, false);
            var legacyPubKey = _hdOps.GeneratePublicKey(legacyExtPubKey, i, false);

            Assert.Equal(
                freshPubKey.GetSegwitAddress(_testnet).ToString(),
                legacyPubKey.GetSegwitAddress(_testnet).ToString());
        }
    }

    [Fact]
    public void TryMigrate_NoExtPubKey_ReturnsFalse()
    {
        var empty = new AccountInfo { walletId = "x", ExtPubKey = null!, RootExtPubKey = null!, Path = null! };
        var migrated = StandardWalletDescriptors.TryMigrate(empty, 1);
        Assert.False(migrated);
    }

    // ── Descriptor format validation ─────────────────────────────────────────

    [Fact]
    public void ExtractXPub_InvalidDescriptor_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            StandardWalletDescriptors.ExtractXPub("p2pkh(tpub/0/*)"));
    }

    [Fact]
    public void Build_EmptyXPub_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            StandardWalletDescriptors.Build("", null, 1));
    }
}
