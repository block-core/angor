using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor.Domain;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using Stage = Angor.Sdk.Funding.Projects.Domain.Stage;

namespace Angor.Sdk.Tests.Funding.Investor.Operations;

public class BuildUnfundedReleaseTransactionTests
{
    private readonly Mock<ISeedwordsProvider> _mockSeedwordsProvider;
    private readonly Mock<IDerivationOperations> _mockDerivationOperations;
    private readonly Mock<IProjectService> _mockProjectService;
    private readonly Mock<IInvestorTransactionActions> _mockInvestorTransactionActions;
    private readonly Mock<IPortfolioService> _mockPortfolioService;
    private readonly Mock<INetworkConfiguration> _mockNetworkConfiguration;
    private readonly Mock<IWalletOperations> _mockWalletOperations;
    private readonly Mock<ISignService> _mockSignService;
    private readonly Mock<IEncryptionService> _mockDecrypter;
    private readonly Mock<ISerializer> _mockSerializer;
    private readonly Mock<ITransactionService> _mockTransactionService;
    private readonly Mock<IWalletAccountBalanceService> _mockWalletAccountBalanceService;
    private readonly BuildUnfundedReleaseTransaction.BuildUnfundedReleaseTransactionHandler _sut;

    public BuildUnfundedReleaseTransactionTests()
    {
        _mockSeedwordsProvider = new Mock<ISeedwordsProvider>();
        _mockDerivationOperations = new Mock<IDerivationOperations>();
        _mockProjectService = new Mock<IProjectService>();
        _mockInvestorTransactionActions = new Mock<IInvestorTransactionActions>();
        _mockPortfolioService = new Mock<IPortfolioService>();
        _mockNetworkConfiguration = new Mock<INetworkConfiguration>();
        _mockWalletOperations = new Mock<IWalletOperations>();
        _mockSignService = new Mock<ISignService>();
        _mockDecrypter = new Mock<IEncryptionService>();
        _mockSerializer = new Mock<ISerializer>();
        _mockTransactionService = new Mock<ITransactionService>();
        _mockWalletAccountBalanceService = new Mock<IWalletAccountBalanceService>();

        _sut = new BuildUnfundedReleaseTransaction.BuildUnfundedReleaseTransactionHandler(
            _mockSeedwordsProvider.Object,
            _mockDerivationOperations.Object,
            _mockProjectService.Object,
            _mockInvestorTransactionActions.Object,
            _mockPortfolioService.Object,
            _mockNetworkConfiguration.Object,
            _mockWalletOperations.Object,
            _mockSignService.Object,
            _mockDecrypter.Object,
            _mockSerializer.Object,
            _mockTransactionService.Object,
            _mockWalletAccountBalanceService.Object);
    }

    [Fact]
    public async Task Handle_WhenProjectNotFound_ReturnsFailure()
    {
        // Arrange
        var request = CreateRequest();

        _mockProjectService
            .Setup(x => x.GetAsync(It.IsAny<ProjectId>()))
            .ReturnsAsync(Result.Failure<Project>("Project not found"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Project not found");
    }

    [Fact]
    public async Task Handle_WhenPortfolioFails_ReturnsFailure()
    {
        // Arrange
        var request = CreateRequest();
        SetupProject();

        _mockPortfolioService
            .Setup(x => x.GetByWalletId("wallet-1"))
            .ReturnsAsync(Result.Failure<InvestmentRecords>("Storage error"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Storage error");
    }

    [Fact]
    public async Task Handle_WhenNoInvestmentFound_ReturnsFailure()
    {
        // Arrange
        var request = CreateRequest();
        SetupProject();

        var records = new InvestmentRecords
        {
            ProjectIdentifiers = new List<InvestmentRecord>()
        };
        _mockPortfolioService
            .Setup(x => x.GetByWalletId("wallet-1"))
            .ReturnsAsync(Result.Success(records));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No investment found");
    }

    [Fact]
    public async Task Handle_WhenSeedwordsProviderFails_ReturnsFailure()
    {
        // Arrange
        var request = CreateRequest();
        SetupProject();
        SetupInvestment();

        _mockSeedwordsProvider
            .Setup(x => x.GetSensitiveData("wallet-1"))
            .ReturnsAsync(Result.Failure<(string Words, Maybe<string> Passphrase)>("Wallet locked"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Wallet locked");
    }

    [Fact]
    public async Task Handle_WhenBalanceServiceFails_ReturnsFailure()
    {
        // Arrange
        var request = CreateRequest();
        SetupProject();
        SetupInvestment();
        SetupSeedwords();

        _mockWalletAccountBalanceService
            .Setup(x => x.GetAccountBalanceInfoAsync(It.IsAny<WalletId>()))
            .ReturnsAsync(Result.Failure<AccountBalanceInfo>("Balance unavailable"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Balance unavailable");
    }

    [Fact]
    public async Task Handle_WhenNoFounderSignaturesFound_ReturnsFailure()
    {
        // Arrange
        var request = CreateRequest();
        SetupProject();
        SetupInvestment();
        SetupSeedwords();
        SetupBalanceService();
        SetupNetwork();

        _mockDerivationOperations
            .Setup(x => x.DeriveInvestorPrivateKey(It.IsAny<WalletWords>(), It.IsAny<string>()))
            .Returns(new Key());

        // LookupReleaseSigs ends without finding signatures
        _mockSignService
            .Setup(x => x.LookupReleaseSigs(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<string>(),
                It.IsAny<Action<string>>(), It.IsAny<Action>()))
            .Callback<string, string, DateTime?, string, Action<string>, Action>(
                (pubKey, projPub, time, eventId, onContent, onEnd) =>
                {
                    onEnd();
                });

        // Also need derivation for lookup
        _mockDerivationOperations
            .Setup(x => x.DeriveNostrPubKey(It.IsAny<WalletWords>(), It.IsAny<string>()))
            .Returns("derived-nostr-pub");

        _mockDerivationOperations
            .Setup(x => x.DeriveProjectNostrPrivateKeyAsync(It.IsAny<WalletWords>(), It.IsAny<string>()))
            .ReturnsAsync(new Key());

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No founder signatures found");
    }

    private static BuildUnfundedReleaseTransaction.BuildUnfundedReleaseTransactionRequest CreateRequest()
    {
        return new BuildUnfundedReleaseTransaction.BuildUnfundedReleaseTransactionRequest(
            new WalletId("wallet-1"),
            new ProjectId("project-1"),
            new DomainFeerate(10));
    }

    private void SetupProject()
    {
        var project = new Project
        {
            Id = new ProjectId("project-1"),
            Name = "Test Project",
            FounderKey = "founder-key",
            FounderRecoveryKey = "recovery-key",
            NostrPubKey = "nostr-pub-key",
            ShortDescription = "Test",
            TargetAmount = 1_000_000,
            StartingDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            EndDate = DateTime.UtcNow.AddYears(1),
            Stages = new List<Stage>()
        };
        _mockProjectService
            .Setup(x => x.GetAsync(It.IsAny<ProjectId>()))
            .ReturnsAsync(Result.Success(project));
    }

    private void SetupInvestment()
    {
        // Must be valid hex that network.CreateTransaction() can parse
        var network = Angor.Shared.Networks.Networks.Bitcoin.Testnet();
        var validTxHex = network.CreateTransaction().ToHex();

        var investment = new InvestmentRecord
        {
            ProjectIdentifier = "project-1",
            InvestmentTransactionHex = validTxHex,
            InvestmentTransactionHash = "tx-hash",
            RequestEventId = "request-event-id",
            UnfundedReleaseAddress = "release-addr"
        };
        var records = new InvestmentRecords
        {
            ProjectIdentifiers = new List<InvestmentRecord> { investment }
        };
        _mockPortfolioService
            .Setup(x => x.GetByWalletId("wallet-1"))
            .ReturnsAsync(Result.Success(records));
    }

    private void SetupSeedwords()
    {
        var sensitiveData = (Words: "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
            Passphrase: Maybe<string>.None);
        _mockSeedwordsProvider
            .Setup(x => x.GetSensitiveData("wallet-1"))
            .ReturnsAsync(Result.Success(sensitiveData));
    }

    private void SetupBalanceService()
    {
        var balanceInfo = new AccountBalanceInfo();
        var accountInfo = new AccountInfo
        {
            ChangeAddressesInfo = new List<AddressInfo>
            {
                new AddressInfo { Address = "change-address-1", HasHistory = false }
            }
        };
        balanceInfo.UpdateAccountBalanceInfo(accountInfo, new List<UtxoData>());
        _mockWalletAccountBalanceService
            .Setup(x => x.GetAccountBalanceInfoAsync(It.IsAny<WalletId>()))
            .ReturnsAsync(Result.Success(balanceInfo));
    }

    private void SetupNetwork()
    {
        var network = Angor.Shared.Networks.Networks.Bitcoin.Testnet();
        _mockNetworkConfiguration
            .Setup(x => x.GetNetwork())
            .Returns(network);
    }
}
