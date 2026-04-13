using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Funding.Shared.TransactionDrafts;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using Nostr.Client.Responses;
using Stage = Angor.Sdk.Funding.Projects.Domain.Stage;

namespace Angor.Sdk.Tests.Funding.Investor.Operations;

public class NotifyFounderOfInvestmentTests
{
    private readonly Mock<IProjectService> _mockProjectService;
    private readonly Mock<ISeedwordsProvider> _mockSeedwordsProvider;
    private readonly Mock<IDerivationOperations> _mockDerivationOperations;
    private readonly Mock<IEncryptionService> _mockEncryptionService;
    private readonly Mock<ISerializer> _mockSerializer;
    private readonly Mock<ISignService> _mockSignService;
    private readonly NotifyFounderOfInvestment.NotifyFounderOfInvestmentHandler _sut;

    public NotifyFounderOfInvestmentTests()
    {
        _mockProjectService = new Mock<IProjectService>();
        _mockSeedwordsProvider = new Mock<ISeedwordsProvider>();
        _mockDerivationOperations = new Mock<IDerivationOperations>();
        _mockEncryptionService = new Mock<IEncryptionService>();
        _mockSerializer = new Mock<ISerializer>();
        _mockSignService = new Mock<ISignService>();

        _sut = new NotifyFounderOfInvestment.NotifyFounderOfInvestmentHandler(
            _mockProjectService.Object,
            _mockSeedwordsProvider.Object,
            _mockDerivationOperations.Object,
            _mockEncryptionService.Object,
            _mockSerializer.Object,
            _mockSignService.Object);
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

        _mockProjectService
            .Setup(x => x.GetAsync(It.IsAny<ProjectId>()))
            .ReturnsAsync(Result.Success(CreateTestProject()));

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
    public async Task Handle_WhenSuccessful_ReturnsEventTimeAndId()
    {
        // Arrange
        var request = CreateRequest();
        SetupSuccessfulFlow();

        var expectedTime = new DateTime(2025, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        var expectedEventId = "nostr-event-abc";

        _mockSignService
            .Setup(x => x.NotifyInvestmentCompleted(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<NostrOkResponse>>()))
            .Returns((expectedTime, expectedEventId));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EventTime.Should().Be(expectedTime);
        result.Value.EventId.Should().Be(expectedEventId);
    }

    [Fact]
    public async Task Handle_WhenSuccessful_SerializesInvestmentNotification()
    {
        // Arrange
        var request = CreateRequest();
        SetupSuccessfulFlow();

        string? capturedSerialized = null;
        _mockSerializer
            .Setup(x => x.Serialize(It.IsAny<InvestmentNotification>()))
            .Callback<object>(obj =>
            {
                var notification = obj as InvestmentNotification;
                capturedSerialized = $"{notification?.ProjectIdentifier}|{notification?.TransactionId}";
            })
            .Returns("serialized-notification");

        _mockSignService
            .Setup(x => x.NotifyInvestmentCompleted(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<NostrOkResponse>>()))
            .Returns((DateTime.UtcNow, "event-id"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedSerialized.Should().Be("project-1|tx-123");
    }

    [Fact]
    public async Task Handle_WhenSignServiceThrows_ReturnsFailure()
    {
        // Arrange
        var request = CreateRequest();
        SetupSuccessfulFlow();

        _mockSignService
            .Setup(x => x.NotifyInvestmentCompleted(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<NostrOkResponse>>()))
            .Throws(new InvalidOperationException("Relay unreachable"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Relay unreachable");
    }

    [Fact]
    public async Task Handle_WhenSuccessful_SendsToProjectNostrPubKey()
    {
        // Arrange
        var request = CreateRequest();

        var project = CreateTestProject();
        project.NostrPubKey = "project-nostr-key-xyz";

        _mockProjectService
            .Setup(x => x.GetAsync(It.IsAny<ProjectId>()))
            .ReturnsAsync(Result.Success(project));

        var sensitiveData = (Words: "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
            Passphrase: Maybe<string>.None);
        _mockSeedwordsProvider
            .Setup(x => x.GetSensitiveData("wallet-1"))
            .ReturnsAsync(Result.Success(sensitiveData));

        _mockDerivationOperations
            .Setup(x => x.DeriveProjectNostrPrivateKeyAsync(It.IsAny<WalletWords>(), It.IsAny<string>()))
            .ReturnsAsync(new Key());

        _mockSerializer
            .Setup(x => x.Serialize(It.IsAny<InvestmentNotification>()))
            .Returns("serialized");

        _mockSignService
            .Setup(x => x.NotifyInvestmentCompleted(
                It.IsAny<string>(), It.IsAny<string>(), "project-nostr-key-xyz", It.IsAny<Action<NostrOkResponse>>()))
            .Returns((DateTime.UtcNow, "event-id"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockSignService.Verify(
            x => x.NotifyInvestmentCompleted(
                It.IsAny<string>(), It.IsAny<string>(), "project-nostr-key-xyz", It.IsAny<Action<NostrOkResponse>>()),
            Times.Once);
    }

    private void SetupSuccessfulFlow()
    {
        _mockProjectService
            .Setup(x => x.GetAsync(It.IsAny<ProjectId>()))
            .ReturnsAsync(Result.Success(CreateTestProject()));

        var sensitiveData = (Words: "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
            Passphrase: Maybe<string>.None);
        _mockSeedwordsProvider
            .Setup(x => x.GetSensitiveData(It.IsAny<string>()))
            .ReturnsAsync(Result.Success(sensitiveData));

        _mockDerivationOperations
            .Setup(x => x.DeriveProjectNostrPrivateKeyAsync(It.IsAny<WalletWords>(), It.IsAny<string>()))
            .ReturnsAsync(new Key());

        _mockSerializer
            .Setup(x => x.Serialize(It.IsAny<InvestmentNotification>()))
            .Returns("serialized");
    }

    private static NotifyFounderOfInvestment.NotifyFounderOfInvestmentRequest CreateRequest()
    {
        var draft = new InvestmentDraft("investor-key")
        {
            SignedTxHex = "signed-hex",
            TransactionId = "tx-123",
            TransactionFee = new Amount(500),
            InvestedAmount = new Amount(50_000)
        };
        return new NotifyFounderOfInvestment.NotifyFounderOfInvestmentRequest(
            new WalletId("wallet-1"), new ProjectId("project-1"), draft);
    }

    private static Project CreateTestProject()
    {
        return new Project
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
    }
}
