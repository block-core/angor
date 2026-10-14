using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using Angor.Shared.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NBitcoin;
using NBitcoin.DataEncoders;

namespace Angor.Test;

/// <summary>
/// Cross-derivation guard: the master key and first receive address derived from a mnemonic must stay
/// byte-identical across every platform (webapp, desktop, CLI). A change anywhere in the derivation chain
/// (BIP-39 seed, BIP-32 master, account path, address encoding) makes these fail.
/// </summary>
public class Bip39CrossDerivationTests
{
    // Official BIP-39 vectors (trezor/python-mnemonic vectors.json), passphrase "TREZOR".
    private const string TrezorPassphrase = "TREZOR";

    // Official Japanese vectors (bip32JP/bip32JP.github.io test_JP_BIP39.json): ideographic-space separated
    // words and a passphrase that only matches after NFKD normalization.
    private const string JapanesePassphrase = "㍍ガバヴァぱばぐゞちぢ十人十色";

    public static IEnumerable<object[]> Vectors()
    {
        yield return new object[]
        {
            "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
            TrezorPassphrase,
            "c55257c360c07c72029aebc1b53c05ed0362ada38ead3e3e9efa3708e53495531f09a6987599d18264c1e1c92f2cf141630c7a3c4ab7c81b2f001698e7463b04",
            "xprv9s21ZrQH143K3h3fDYiay8mocZ3afhfULfb5GX8kCBdno77K4HiA15Tg23wpbeF1pLfs1c5SPmYHrEpTuuRhxMwvKDwqdKiGJS9XFKzUsAF",
            "bc1qv5rmq0kt9yz3pm36wvzct7p3x6mtgehjul0feu"
        };
        yield return new object[]
        {
            "legal winner thank year wave sausage worth useful legal winner thank yellow",
            TrezorPassphrase,
            "2e8905819b8723fe2c1d161860e5ee1830318dbf49a83bd451cfb8440c28bd6fa457fe1296106559a3c80937a1c1069be3a3a5bd381ee6260e8d9739fce1f607",
            "xprv9s21ZrQH143K2gA81bYFHqU68xz1cX2APaSq5tt6MFSLeXnCKV1RVUJt9FWNTbrrryem4ZckN8k4Ls1H6nwdvDTvnV7zEXs2HgPezuVccsq",
            "bc1qcgphwddfcv5pax0agdw697rd8ewde8r02nwrex"
        };
        yield return new object[]
        {
            "zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo wrong",
            TrezorPassphrase,
            "ac27495480225222079d7be181583751e86f571027b0497b5b5d11218e0a8a13332572917f0f8e5a589620c6f15b11c61dee327651a14c34e18231052e48c069",
            "xprv9s21ZrQH143K2V4oox4M8Zmhi2Fjx5XK4Lf7GKRvPSgydU3mjZuKGCTg7UPiBUD7ydVPvSLtg9hjp7MQTYsW67rZHAXeccqYqrsx8LcXnyd",
            "bc1qeruu9tjx0ux75w98mhwsz867w5hp5259630tt4"
        };
        yield return new object[]
        {
            "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon art",
            TrezorPassphrase,
            "bda85446c68413707090a52022edd26a1c9462295029f2e60cd7c4f2bbd3097170af7a4d73245cafa9c3cca8d561a7c3de6f5d4a10be8ed2a5e608d68f92fcc8",
            "xprv9s21ZrQH143K32qBagUJAMU2LsHg3ka7jqMcV98Y7gVeVyNStwYS3U7yVVoDZ4btbRNf4h6ibWpY22iRmXq35qgLs79f312g2kj5539ebPM",
            "bc1q8ee9jp2ga509lae7xdcu88j0elzlyv0khhryaq"
        };
        yield return new object[]
        {
            "あいこくしん　あいこくしん　あいこくしん　あいこくしん　あいこくしん　あいこくしん　あいこくしん　あいこくしん　あいこくしん　あいこくしん　あいこくしん　あおぞら",
            JapanesePassphrase,
            "a262d6fb6122ecf45be09c50492b31f92e9beb7d9a845987a02cefda57a15f9c467a17872029a9e92299b5cbdf306e3a0ee620245cbd508959b6cb7ca637bd55",
            "xprv9s21ZrQH143K258jAiWPAM6JYT9hLA91MV3AZUKfxmLZJCjCHeSjBvMbDy8C1mJ2FL5ytExyS97FAe6pQ6SD5Jt9SwHaLorA8i5Eojokfo1",
            "bc1q4arcqm0tra7q8uuyxs769yz9ts2q3fzjevr8k0"
        };
        yield return new object[]
        {
            "そつう　れきだい　ほんやく　わかす　りくつ　ばいか　ろせん　やちん　そつう　れきだい　ほんやく　わかめ",
            JapanesePassphrase,
            "aee025cbe6ca256862f889e48110a6a382365142f7d16f2b9545285b3af64e542143a577e9c144e101a6bdca18f8d97ec3366ebf5b088b1c1af9bc31346e60d9",
            "xprv9s21ZrQH143K3ra1D6uGQyST9UqtUscH99GK8MBh5RrgPkrQo83QG4o6H2YktwSKvoZRVXDQZQrSyCDpHdA2j8i3PW5M9LkauaaTKwym1Wf",
            "bc1qwwx53kr067k33wrxvyxu45f782ch6yza4jfku3"
        };
        yield return new object[]
        {
            "そとづら　あまど　おおう　あこがれる　いくぶん　けいけん　あたえる　いよく　そとづら　あまど　おおう　あかちゃん",
            JapanesePassphrase,
            "e51736736ebdf77eda23fa17e31475fa1d9509c78f1deb6b4aacfbd760a7e2ad769c714352c95143b5c1241985bcb407df36d64e75dd5a2b78ca5d2ba82a3544",
            "xprv9s21ZrQH143K2aDKfG8hpfvRXzANmyBQWoqoUXWaSwVZcKtnmX5xTVkkHAdD9yykuuBcagjCFK6iLcBdHHxXC1g3TT9xHSu4PW6SRf3KvVy",
            "bc1qd9eeh5wv48zk4gdqscq05p23tghvgya535jwt4"
        };
    }

    [Theory]
    [MemberData(nameof(Vectors))]
    public void Derivation_ForOfficialVector_MatchesSeedXprvAndFirstReceiveAddress(
        string mnemonic, string passphrase, string expectedSeedHex, string expectedXprv, string expectedFirstAddress)
    {
        // Arrange
        var hdOperations = new HdOperations();

        // Act
        ExtKey key = hdOperations.GetExtendedKey(mnemonic, passphrase);
        string firstAddress = DeriveFirstReceiveAddress(mnemonic, passphrase, Networks.Bitcoin.Mainnet());

        // Assert
        Encoders.Hex.EncodeData(new Mnemonic(mnemonic).DeriveSeed(passphrase)).Should().Be(expectedSeedHex);
        key.ToString(Network.Main).Should().Be(expectedXprv);
        firstAddress.Should().Be(expectedFirstAddress);
    }
    [Fact]
    public void BuildAccountInfoForWalletWords_ForBip84ReferenceMnemonic_MatchesPublishedAddress()
    {
        // Arrange - BIP-84 reference: m/84'/0'/0'/0/0 of the all-"abandon" mnemonic, no passphrase.
        AngorNetwork network = Networks.Bitcoin.Mainnet();

        // Act
        string address = DeriveFirstReceiveAddress(
            "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
            null,
            network);

        // Assert
        address.Should().Be("bc1qcr8te4kr609gcawutmrza0j4xv80jy8z306fyu");
    }

    [Fact]
    public void BuildAccountInfoForWalletWords_WhenPassphraseDiffers_YieldsDifferentAddress()
    {
        // Arrange
        const string mnemonic =
            "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
        AngorNetwork network = Networks.Bitcoin.Mainnet();

        // Act
        string withoutPassphrase = DeriveFirstReceiveAddress(mnemonic, null, network);
        string withPassphrase = DeriveFirstReceiveAddress(mnemonic, TrezorPassphrase, network);

        // Assert
        withPassphrase.Should().NotBe(withoutPassphrase);
    }

    private static string DeriveFirstReceiveAddress(string mnemonic, string? passphrase, AngorNetwork network)
    {
        var networkConfiguration = new Mock<INetworkConfiguration>();
        networkConfiguration.Setup(n => n.GetNetwork()).Returns(network);

        var hdOperations = new HdOperations();
        var walletOperations = new WalletOperations(
            new Mock<IIndexerService>().Object,
            hdOperations,
            NullLogger<WalletOperations>.Instance,
            networkConfiguration.Object);

        using var walletWords = new WalletWords { Words = mnemonic, Passphrase = passphrase };

        AccountInfo accountInfo = walletOperations.BuildAccountInfoForWalletWords(walletWords);

        ExtPubKey accountPubKey = ExtPubKey.Parse(accountInfo.ExtPubKey, network.BitcoinNetwork);
        PubKey firstKey = hdOperations.GeneratePublicKey(accountPubKey, 0, false);

        return firstKey.GetAddress(ScriptPubKeyType.Segwit, network.BitcoinNetwork).ToString();
    }
}
