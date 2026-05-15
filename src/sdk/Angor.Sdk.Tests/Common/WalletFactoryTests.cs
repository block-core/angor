using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Domain;
using Angor.Sdk.Wallet.Infrastructure.Impl;
using Angor.Sdk.Wallet.Infrastructure.Interfaces;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Data.Documents.Interfaces;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using Xunit;

namespace Angor.Sdk.Tests.Common;

public class WalletFactoryTests
{
    private readonly Mock<IWalletStore> _walletStore = new();
    private readonly Mock<ISensitiveWalletDataProvider> _sensitiveWalletDataProvider = new();
    private readonly Mock<IWalletOperations> _walletOperations = new();
    private readonly Mock<IWalletAccountBalanceService> _accountBalanceService = new();
    private readonly Mock<IDerivationOperations> _derivationOperations = new();
    private readonly Mock<INetworkConfiguration> _networkConfiguration = new();
    private readonly Mock<IGenericDocumentCollection<DerivedProjectKeys>> _derivedProjectKeysCollection = new();
    private readonly Mock<IWalletEncryption> _walletEncryption = new();
    private readonly InMemorySecureKeyProvider _secureKeyProvider = new();

    private WalletFactory CreateSut() => new(
        _walletStore.Object,
        _sensitiveWalletDataProvider.Object,
        _walletOperations.Object,
        _accountBalanceService.Object,
        _derivationOperations.Object,
        _networkConfiguration.Object,
        _derivedProjectKeysCollection.Object,
        _walletEncryption.Object,
        _secureKeyProvider);

    [Fact]
    public async Task CreateWallet_WhenWalletAlreadyExists_DoesNotOverwriteEncryptionKey()
    {
        // Arrange
        var seedWords = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
        var passphrase = Maybe<string>.None;
        var walletId = new WalletId("existing-wallet-id");
        var originalKey = "original-encryption-key-base64==";

        _walletOperations
            .Setup(x => x.BuildAccountInfoForWalletWords(It.IsAny<WalletWords>()))
            .Returns(new AccountInfo { walletId = walletId.Value, ExtPubKey = "xpub", RootExtPubKey = "root", Path = "m/84'/1'/0'" });

        // Pre-save the original key (simulates a wallet that was previously created)
        await _secureKeyProvider.Save(walletId, originalKey);

        // Wallet already exists (has account balance info)
        _accountBalanceService
            .Setup(x => x.GetAccountBalanceInfoAsync(walletId))
            .ReturnsAsync(Result.Success(new AccountBalanceInfo()));

        // Act
        var sut = CreateSut();
        var result = await sut.CreateWallet("Test Wallet", seedWords, passphrase, BitcoinNetwork.Testnet);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // The original key should still be in the key store, NOT overwritten
        var storedKey = await _secureKeyProvider.Get(walletId);
        storedKey.HasValue.Should().BeTrue();
        storedKey.Value.Should().Be(originalKey, "the encryption key must not be overwritten when the wallet already exists");
    }

    [Fact]
    public async Task CreateWallet_WhenWalletIsNew_GeneratesNewEncryptionKey()
    {
        // Arrange
        var seedWords = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
        var passphrase = Maybe<string>.None;
        var walletId = new WalletId("new-wallet-id");

        _walletOperations
            .Setup(x => x.BuildAccountInfoForWalletWords(It.IsAny<WalletWords>()))
            .Returns(new AccountInfo { walletId = walletId.Value, ExtPubKey = "xpub", RootExtPubKey = "root", Path = "m/84'/1'/0'" });

        // Wallet does NOT exist
        _accountBalanceService
            .Setup(x => x.GetAccountBalanceInfoAsync(walletId))
            .ReturnsAsync(Result.Failure<AccountBalanceInfo>("Not found"));

        _walletStore
            .Setup(x => x.GetAll())
            .ReturnsAsync(Result.Success<IEnumerable<EncryptedWallet>>([]));

        _walletStore
            .Setup(x => x.SaveAll(It.IsAny<IEnumerable<EncryptedWallet>>()))
            .ReturnsAsync(Result.Success());

        _walletEncryption
            .Setup(x => x.Encrypt(It.IsAny<WalletData>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new EncryptedWallet());

        _accountBalanceService
            .Setup(x => x.SaveAccountBalanceInfoAsync(walletId, It.IsAny<AccountBalanceInfo>()))
            .ReturnsAsync(Result.Success());

        _derivedProjectKeysCollection
            .Setup(x => x.UpsertAsync(It.IsAny<Func<DerivedProjectKeys, string>>(), It.IsAny<DerivedProjectKeys>()))
            .ReturnsAsync(Result.Success(true));

        _derivationOperations
            .Setup(x => x.DeriveProjectKeys(It.IsAny<WalletWords>(), It.IsAny<string>()))
            .Returns(new FounderKeyCollection { Keys = [] });

        _networkConfiguration
            .Setup(x => x.GetAngorKey())
            .Returns("angor-key");

        // Act
        var sut = CreateSut();
        var result = await sut.CreateWallet("Test Wallet", seedWords, passphrase, BitcoinNetwork.Testnet);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var storedKey = await _secureKeyProvider.Get(walletId);
        storedKey.HasValue.Should().BeTrue();
        storedKey.Value.Should().NotBeNullOrEmpty("a new encryption key should be generated for a new wallet");
    }
}
