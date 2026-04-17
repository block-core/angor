using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Models;
using Blockcore.NBitcoin;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;

namespace Angor.Sdk.Tests.Funding.Investor.Operations;

public class GetInvestorNsecTests
{
    private readonly Mock<ISeedwordsProvider> _mockSeedwordsProvider;
    private readonly Mock<IDerivationOperations> _mockDerivationOperations;
    private readonly GetInvestorNsec.GetInvestorNsecHandler _sut;

    public GetInvestorNsecTests()
    {
        _mockSeedwordsProvider = new Mock<ISeedwordsProvider>();
        _mockDerivationOperations = new Mock<IDerivationOperations>();
        _sut = new GetInvestorNsec.GetInvestorNsecHandler(
            _mockSeedwordsProvider.Object,
            _mockDerivationOperations.Object);
    }

    [Fact]
    public async Task Handle_WhenSeedwordsProviderFails_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var request = new GetInvestorNsec.GetInvestorNsecRequest(walletId, "founder-key-123");

        _mockSeedwordsProvider
            .Setup(x => x.GetSensitiveData(walletId.Value))
            .ReturnsAsync(Result.Failure<(string Words, Maybe<string> Passphrase)>("Wallet locked"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Wallet locked");
        _mockDerivationOperations.Verify(
            x => x.DeriveProjectNostrPrivateKeyAsync(It.IsAny<WalletWords>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSuccessful_ReturnsNsecString()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var founderKey = "founder-key-123";
        var request = new GetInvestorNsec.GetInvestorNsecRequest(walletId, founderKey);

        var sensitiveData = (Words: "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
            Passphrase: Maybe<string>.None);
        _mockSeedwordsProvider
            .Setup(x => x.GetSensitiveData(walletId.Value))
            .ReturnsAsync(Result.Success(sensitiveData));

        // Use a real 32-byte key for test
        var testKey = new Key();
        _mockDerivationOperations
            .Setup(x => x.DeriveProjectNostrPrivateKeyAsync(It.IsAny<WalletWords>(), founderKey))
            .ReturnsAsync(testKey);

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Nsec.Should().StartWith("nsec1", "Nostr nsec keys start with 'nsec1' prefix");
        result.Value.Nsec.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_PassesCorrectFounderKeyToDerivation()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var founderKey = "specific-founder-key";
        var request = new GetInvestorNsec.GetInvestorNsecRequest(walletId, founderKey);

        var sensitiveData = (Words: "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
            Passphrase: Maybe<string>.None);
        _mockSeedwordsProvider
            .Setup(x => x.GetSensitiveData(walletId.Value))
            .ReturnsAsync(Result.Success(sensitiveData));

        var testKey = new Key();
        _mockDerivationOperations
            .Setup(x => x.DeriveProjectNostrPrivateKeyAsync(It.IsAny<WalletWords>(), "specific-founder-key"))
            .ReturnsAsync(testKey);

        // Act
        await _sut.Handle(request, CancellationToken.None);

        // Assert
        _mockDerivationOperations.Verify(
            x => x.DeriveProjectNostrPrivateKeyAsync(It.IsAny<WalletWords>(), "specific-founder-key"),
            Times.Once);
    }
}
