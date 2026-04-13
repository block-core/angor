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
using Serilog;
using Stage = Angor.Sdk.Funding.Projects.Domain.Stage;

namespace Angor.Sdk.Tests.Funding.Investor.Operations;

public class PublishInvestmentTests
{
    private readonly Mock<INetworkConfiguration> _mockNetworkConfiguration;
    private readonly Mock<ISignService> _mockSignService;
    private readonly Mock<IEncryptionService> _mockDecrypter;
    private readonly Mock<ISerializer> _mockSerializer;
    private readonly Mock<IInvestorTransactionActions> _mockInvestorTransactionActions;
    private readonly Mock<IDerivationOperations> _mockDerivationOperations;
    private readonly Mock<ISeedwordsProvider> _mockSeedwordsProvider;
    private readonly Mock<IProjectService> _mockProjectService;
    private readonly Mock<IWalletOperations> _mockWalletOperations;
    private readonly Mock<IPortfolioService> _mockPortfolioService;
    private readonly Mock<IWalletAccountBalanceService> _mockWalletAccountBalanceService;
    private readonly Mock<ILogger> _mockLogger;
    private readonly PublishInvestment.PublishInvestmentHandler _sut;

    public PublishInvestmentTests()
    {
        _mockNetworkConfiguration = new Mock<INetworkConfiguration>();
        _mockSignService = new Mock<ISignService>();
        _mockDecrypter = new Mock<IEncryptionService>();
        _mockSerializer = new Mock<ISerializer>();
        _mockInvestorTransactionActions = new Mock<IInvestorTransactionActions>();
        _mockDerivationOperations = new Mock<IDerivationOperations>();
        _mockSeedwordsProvider = new Mock<ISeedwordsProvider>();
        _mockProjectService = new Mock<IProjectService>();
        _mockWalletOperations = new Mock<IWalletOperations>();
        _mockPortfolioService = new Mock<IPortfolioService>();
        _mockWalletAccountBalanceService = new Mock<IWalletAccountBalanceService>();
        _mockLogger = new Mock<ILogger>();

        _sut = new PublishInvestment.PublishInvestmentHandler(
            _mockNetworkConfiguration.Object,
            _mockSignService.Object,
            _mockDecrypter.Object,
            _mockSerializer.Object,
            _mockInvestorTransactionActions.Object,
            _mockDerivationOperations.Object,
            _mockSeedwordsProvider.Object,
            _mockProjectService.Object,
            _mockWalletOperations.Object,
            _mockPortfolioService.Object,
            _mockWalletAccountBalanceService.Object,
            _mockLogger.Object);
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
    public async Task Handle_WhenNoInvestmentInPortfolio_ReturnsFailure()
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
        result.Error.Should().Contain("investment transaction was not found in storage");
    }

    [Fact]
    public async Task Handle_WhenInvestmentIdDoesNotMatch_ReturnsFailure()
    {
        // Arrange
        var request = CreateRequest();
        SetupProject();

        var investment = new InvestmentRecord
        {
            ProjectIdentifier = "project-1",
            InvestmentTransactionHex = "tx-hex",
            InvestmentTransactionHash = "different-tx-hash",
            RequestEventId = "event-id",
            RequestEventTime = DateTime.UtcNow
        };
        var records = new InvestmentRecords
        {
            ProjectIdentifiers = new List<InvestmentRecord> { investment }
        };
        _mockPortfolioService
            .Setup(x => x.GetByWalletId("wallet-1"))
            .ReturnsAsync(Result.Success(records));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to find the investment transaction with the given ID");
    }

    [Fact]
    public async Task Handle_WhenTransactionPublishFails_ReturnsFailure()
    {
        // Arrange
        var request = CreateRequest();
        SetupProject();
        SetupSeedwords();
        SetupDerivation();

        var network = Angor.Shared.Networks.Networks.Bitcoin.Testnet();
        var tx = network.CreateTransaction();
        var txHash = tx.GetHash().ToString();

        _mockNetworkConfiguration
            .Setup(x => x.GetNetwork())
            .Returns(network);

        var investment = new InvestmentRecord
        {
            ProjectIdentifier = "project-1",
            InvestmentTransactionHex = tx.ToHex(),
            InvestmentTransactionHash = txHash,
            RequestEventId = "event-id",
            RequestEventTime = DateTime.UtcNow
        };
        var records = new InvestmentRecords
        {
            ProjectIdentifiers = new List<InvestmentRecord> { investment }
        };
        _mockPortfolioService
            .Setup(x => x.GetByWalletId("wallet-1"))
            .ReturnsAsync(Result.Success(records));

        // Signature validation via TaskCompletionSource — simulate onEnd with no content (returns false)
        _mockSignService
            .Setup(x => x.LookupSignatureForInvestmentRequest(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<string>(),
                It.IsAny<Func<string, Task>>(), It.IsAny<Action>()))
            .Callback<string, string, DateTime?, string, Func<string, Task>, Action>(
                (pubKey, projPub, time, eventId, onContent, onEnd) =>
                {
                    onEnd();
                });

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert — validation returned false → failure
        result.IsFailure.Should().BeTrue();
    }

    private static PublishInvestment.PublishInvestmentRequest CreateRequest()
    {
        return new PublishInvestment.PublishInvestmentRequest(
            "tx-hash-123",
            new WalletId("wallet-1"),
            new ProjectId("project-1"));
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

    private void SetupSeedwords()
    {
        var sensitiveData = (Words: "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
            Passphrase: Maybe<string>.None);
        _mockSeedwordsProvider
            .Setup(x => x.GetSensitiveData("wallet-1"))
            .ReturnsAsync(Result.Success(sensitiveData));
    }

    private void SetupDerivation()
    {
        _mockDerivationOperations
            .Setup(x => x.DeriveNostrPubKey(It.IsAny<WalletWords>(), It.IsAny<string>()))
            .Returns("derived-nostr-pub");

        _mockDerivationOperations
            .Setup(x => x.DeriveProjectNostrPrivateKeyAsync(It.IsAny<WalletWords>(), It.IsAny<string>()))
            .ReturnsAsync(new Key());
    }
}
