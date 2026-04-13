using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
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

public class NotifyFounderOfCancellationTests
{
    private readonly Mock<IProjectService> _mockProjectService;
    private readonly Mock<ISeedwordsProvider> _mockSeedwordsProvider;
    private readonly Mock<IDerivationOperations> _mockDerivationOperations;
    private readonly Mock<ISerializer> _mockSerializer;
    private readonly Mock<ISignService> _mockSignService;
    private readonly NotifyFounderOfCancellation.NotifyFounderOfCancellationHandler _sut;

    public NotifyFounderOfCancellationTests()
    {
        _mockProjectService = new Mock<IProjectService>();
        _mockSeedwordsProvider = new Mock<ISeedwordsProvider>();
        _mockDerivationOperations = new Mock<IDerivationOperations>();
        _mockSerializer = new Mock<ISerializer>();
        _mockSignService = new Mock<ISignService>();

        _sut = new NotifyFounderOfCancellation.NotifyFounderOfCancellationHandler(
            _mockProjectService.Object,
            _mockSeedwordsProvider.Object,
            _mockDerivationOperations.Object,
            _mockSerializer.Object,
            _mockSignService.Object);
    }

    [Fact]
    public async Task Handle_WhenProjectNotFound_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var projectId = new ProjectId("nonexistent");
        var request = new NotifyFounderOfCancellation.NotifyFounderOfCancellationRequest(
            walletId, projectId, "event-123");

        _mockProjectService
            .Setup(x => x.GetAsync(projectId))
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
        var walletId = new WalletId("wallet-1");
        var projectId = new ProjectId("project-1");
        var request = new NotifyFounderOfCancellation.NotifyFounderOfCancellationRequest(
            walletId, projectId, "event-123");

        _mockProjectService
            .Setup(x => x.GetAsync(projectId))
            .ReturnsAsync(Result.Success(CreateTestProject(projectId)));

        _mockSeedwordsProvider
            .Setup(x => x.GetSensitiveData(walletId.Value))
            .ReturnsAsync(Result.Failure<(string Words, Maybe<string> Passphrase)>("Wallet locked"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Wallet locked");
    }

    [Fact]
    public async Task Handle_WhenSuccessful_SerializesCancellationNotification()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var projectId = new ProjectId("project-1");
        var requestEventId = "event-123";
        var request = new NotifyFounderOfCancellation.NotifyFounderOfCancellationRequest(
            walletId, projectId, requestEventId);

        SetupSuccessfulFlow(projectId);

        string? capturedContent = null;
        _mockSerializer
            .Setup(x => x.Serialize(It.IsAny<CancellationNotification>()))
            .Callback<object>(obj =>
            {
                var notification = obj as CancellationNotification;
                capturedContent = $"{notification?.ProjectIdentifier}|{notification?.RequestEventId}";
            })
            .Returns("serialized-content");

        _mockSignService
            .Setup(x => x.NotifyInvestmentCanceled(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<NostrOkResponse>>()))
            .Returns((DateTime.UtcNow, "result-event-id"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedContent.Should().Be("project-1|event-123");
    }

    [Fact]
    public async Task Handle_WhenSuccessful_ReturnsEventTimeAndId()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var projectId = new ProjectId("project-1");
        var request = new NotifyFounderOfCancellation.NotifyFounderOfCancellationRequest(
            walletId, projectId, "event-123");

        SetupSuccessfulFlow(projectId);

        var expectedTime = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var expectedEventId = "nostr-event-abc";

        _mockSerializer
            .Setup(x => x.Serialize(It.IsAny<CancellationNotification>()))
            .Returns("serialized");

        _mockSignService
            .Setup(x => x.NotifyInvestmentCanceled(
                "serialized", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<NostrOkResponse>>()))
            .Returns((expectedTime, expectedEventId));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EventTime.Should().Be(expectedTime);
        result.Value.EventId.Should().Be(expectedEventId);
    }

    [Fact]
    public async Task Handle_WhenSuccessful_SendsToProjectNostrPubKey()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var projectId = new ProjectId("project-1");
        var request = new NotifyFounderOfCancellation.NotifyFounderOfCancellationRequest(
            walletId, projectId, "event-123");

        var project = CreateTestProject(projectId);
        project.NostrPubKey = "project-nostr-pubkey-abc";

        _mockProjectService
            .Setup(x => x.GetAsync(projectId))
            .ReturnsAsync(Result.Success(project));

        var sensitiveData = (Words: "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
            Passphrase: Maybe<string>.None);
        _mockSeedwordsProvider
            .Setup(x => x.GetSensitiveData(walletId.Value))
            .ReturnsAsync(Result.Success(sensitiveData));

        _mockDerivationOperations
            .Setup(x => x.DeriveProjectNostrPrivateKeyAsync(It.IsAny<WalletWords>(), project.FounderKey))
            .ReturnsAsync(new Key());

        _mockSerializer
            .Setup(x => x.Serialize(It.IsAny<CancellationNotification>()))
            .Returns("serialized");

        _mockSignService
            .Setup(x => x.NotifyInvestmentCanceled(
                It.IsAny<string>(), It.IsAny<string>(), "project-nostr-pubkey-abc", It.IsAny<Action<NostrOkResponse>>()))
            .Returns((DateTime.UtcNow, "event-id"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockSignService.Verify(
            x => x.NotifyInvestmentCanceled(
                It.IsAny<string>(), It.IsAny<string>(), "project-nostr-pubkey-abc", It.IsAny<Action<NostrOkResponse>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenSignServiceThrows_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var projectId = new ProjectId("project-1");
        var request = new NotifyFounderOfCancellation.NotifyFounderOfCancellationRequest(
            walletId, projectId, "event-123");

        SetupSuccessfulFlow(projectId);

        _mockSerializer
            .Setup(x => x.Serialize(It.IsAny<CancellationNotification>()))
            .Returns("serialized");

        _mockSignService
            .Setup(x => x.NotifyInvestmentCanceled(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<NostrOkResponse>>()))
            .Throws(new InvalidOperationException("Nostr relay unreachable"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Nostr relay unreachable");
    }

    private void SetupSuccessfulFlow(ProjectId projectId)
    {
        var project = CreateTestProject(projectId);
        _mockProjectService
            .Setup(x => x.GetAsync(projectId))
            .ReturnsAsync(Result.Success(project));

        var sensitiveData = (Words: "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
            Passphrase: Maybe<string>.None);
        _mockSeedwordsProvider
            .Setup(x => x.GetSensitiveData(It.IsAny<string>()))
            .ReturnsAsync(Result.Success(sensitiveData));

        _mockDerivationOperations
            .Setup(x => x.DeriveProjectNostrPrivateKeyAsync(It.IsAny<WalletWords>(), It.IsAny<string>()))
            .ReturnsAsync(new Key());
    }

    private static Project CreateTestProject(ProjectId projectId)
    {
        return new Project
        {
            Id = projectId,
            Name = "Test Project",
            FounderKey = "founder-key-abc",
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
