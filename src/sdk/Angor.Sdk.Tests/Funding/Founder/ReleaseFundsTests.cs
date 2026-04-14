using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder.Domain;
using Angor.Sdk.Funding.Founder.Operations;
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

namespace Angor.Sdk.Tests.Funding.Founder;

public class ReleaseFundsTests
{
    private readonly Mock<ISignService> _mockSignService;
    private readonly Mock<IProjectService> _mockProjectService;
    private readonly Mock<INostrDecrypter> _mockNostrDecrypter;
    private readonly Mock<ISerializer> _mockSerializer;
    private readonly Mock<IDerivationOperations> _mockDerivationOperations;
    private readonly Mock<IInvestorTransactionActions> _mockInvestorTransactionActions;
    private readonly Mock<INetworkConfiguration> _mockNetworkConfiguration;
    private readonly Mock<IFounderTransactionActions> _mockFounderTransactionActions;
    private readonly Mock<IEncryptionService> _mockEncryptionService;
    private readonly Mock<ISeedwordsProvider> _mockSeedwordsProvider;
    private readonly ReleaseFunds.ReleaseFundsHandler _sut;

    public ReleaseFundsTests()
    {
        _mockSignService = new Mock<ISignService>();
        _mockProjectService = new Mock<IProjectService>();
        _mockNostrDecrypter = new Mock<INostrDecrypter>();
        _mockSerializer = new Mock<ISerializer>();
        _mockDerivationOperations = new Mock<IDerivationOperations>();
        _mockInvestorTransactionActions = new Mock<IInvestorTransactionActions>();
        _mockNetworkConfiguration = new Mock<INetworkConfiguration>();
        _mockFounderTransactionActions = new Mock<IFounderTransactionActions>();
        _mockEncryptionService = new Mock<IEncryptionService>();
        _mockSeedwordsProvider = new Mock<ISeedwordsProvider>();

        _sut = new ReleaseFunds.ReleaseFundsHandler(
            _mockSignService.Object,
            _mockProjectService.Object,
            _mockNostrDecrypter.Object,
            _mockSerializer.Object,
            _mockDerivationOperations.Object,
            _mockInvestorTransactionActions.Object,
            _mockNetworkConfiguration.Object,
            _mockFounderTransactionActions.Object,
            _mockEncryptionService.Object,
            _mockSeedwordsProvider.Object);
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
    public async Task Handle_WhenSeedwordsProviderFails_ReturnsFailure()
    {
        // Arrange
        var request = CreateRequest();

        SetupProject();
        SetupSignServiceWithNoItems();

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
    public async Task Handle_WhenNoInvestmentEvents_ReturnsSuccess()
    {
        // Arrange
        var request = new ReleaseFunds.ReleaseFundsRequest(
            new WalletId("wallet-1"),
            new ProjectId("project-1"),
            Enumerable.Empty<string>());

        SetupProject();
        SetupSignServiceWithNoItems();
        SetupSeedwords();
        SetupDerivation();

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenSignatureCheckFails_ReturnsFailure()
    {
        // Arrange
        var request = CreateRequest();
        SetupProject();
        SetupSeedwords();
        SetupDerivation();

        var network = Angor.Shared.Networks.Networks.Bitcoin.Testnet();
        _mockNetworkConfiguration
            .Setup(x => x.GetNetwork())
            .Returns(network);

        // Setup sign service to return one item matching our event ID
        _mockSignService
            .Setup(x => x.LookupInvestmentRequestsAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime?>(),
                It.IsAny<Action<string, string, string, DateTime>>(),
                It.IsAny<Action>()))
            .Returns<string, string?, DateTime?, Action<string, string, string, DateTime>, Action>(
                (pubKey, sender, since, onMessage, onEnd) =>
                {
                    onMessage("event-1", "investor-pub-1", "encrypted-msg", DateTime.UtcNow);
                    onEnd();
                    return Task.CompletedTask;
                });

        // Decrypt returns success
        _mockNostrDecrypter
            .Setup(x => x.Decrypt(It.IsAny<WalletId>(), It.IsAny<ProjectId>(), It.IsAny<DirectMessage>()))
            .ReturnsAsync(Result.Success("decrypted-json"));

        // Deserialize returns a valid sign recovery request
        _mockSerializer
            .Setup(x => x.Deserialize<SignRecoveryRequest>(It.IsAny<string>()))
            .Returns(new SignRecoveryRequest
            {
                ProjectIdentifier = "project-1",
                InvestmentTransactionHex = "tx-hex",
                UnfundedReleaseAddress = "release-address"
            });

        // Signature check fails
        _mockInvestorTransactionActions
            .Setup(x => x.CheckInvestorUnfundedReleaseSignatures(
                It.IsAny<ProjectInfo>(),
                It.IsAny<Blockcore.Consensus.TransactionInfo.Transaction>(),
                It.IsAny<SignatureInfo>(),
                It.IsAny<string>()))
            .Returns(false);

        _mockFounderTransactionActions
            .Setup(x => x.SignInvestorRecoveryTransactions(
                It.IsAny<ProjectInfo>(), It.IsAny<string>(),
                It.IsAny<Blockcore.Consensus.TransactionInfo.Transaction>(), It.IsAny<string>()))
            .Returns(new SignatureInfo());

        _mockInvestorTransactionActions
            .Setup(x => x.BuildUnfundedReleaseInvestorFundsTransaction(
                It.IsAny<ProjectInfo>(),
                It.IsAny<Blockcore.Consensus.TransactionInfo.Transaction>(),
                It.IsAny<string>()))
            .Returns(network.CreateTransaction());

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert - the handler catches the InvalidOperationException and appends to failedSignatures
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenDecryptionFails_SkipsItem()
    {
        // Arrange
        var request = CreateRequest();
        SetupProject();
        SetupSeedwords();
        SetupDerivation();

        _mockSignService
            .Setup(x => x.LookupInvestmentRequestsAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime?>(),
                It.IsAny<Action<string, string, string, DateTime>>(),
                It.IsAny<Action>()))
            .Returns<string, string?, DateTime?, Action<string, string, string, DateTime>, Action>(
                (pubKey, sender, since, onMessage, onEnd) =>
                {
                    onMessage("event-1", "investor-pub-1", "encrypted-msg", DateTime.UtcNow);
                    onEnd();
                    return Task.CompletedTask;
                });

        // Decrypt fails — this item should be skipped
        _mockNostrDecrypter
            .Setup(x => x.Decrypt(It.IsAny<WalletId>(), It.IsAny<ProjectId>(), It.IsAny<DirectMessage>()))
            .ReturnsAsync(Result.Failure<string>("Decryption failed"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert — no items to process, should return success
        result.IsSuccess.Should().BeTrue();
    }

    private static ReleaseFunds.ReleaseFundsRequest CreateRequest()
    {
        return new ReleaseFunds.ReleaseFundsRequest(
            new WalletId("wallet-1"),
            new ProjectId("project-1"),
            new List<string> { "event-1" });
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

    private void SetupSignServiceWithNoItems()
    {
        _mockSignService
            .Setup(x => x.LookupInvestmentRequestsAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime?>(),
                It.IsAny<Action<string, string, string, DateTime>>(),
                It.IsAny<Action>()))
            .Returns<string, string?, DateTime?, Action<string, string, string, DateTime>, Action>(
                (pubKey, sender, since, onMessage, onEnd) =>
                {
                    onEnd();
                    return Task.CompletedTask;
                });
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
            .Setup(x => x.DeriveFounderRecoveryPrivateKey(It.IsAny<WalletWords>(), It.IsAny<string>()))
            .Returns(new Key());

        _mockDerivationOperations
            .Setup(x => x.DeriveProjectNostrPrivateKeyAsync(It.IsAny<WalletWords>(), It.IsAny<string>()))
            .ReturnsAsync(new Key());
    }
}
