using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder.Domain;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using Stage = Angor.Sdk.Funding.Projects.Domain.Stage;

namespace Angor.Sdk.Tests.Funding.Founder;

public class GetReleasableTransactionsTests
{
    private readonly Mock<ISignService> _mockSignService;
    private readonly Mock<IProjectService> _mockProjectService;
    private readonly Mock<INostrDecrypter> _mockNostrDecrypter;
    private readonly Mock<ISerializer> _mockSerializer;
    private readonly GetReleasableTransactions.GetClaimableTransactionsHandler _sut;

    public GetReleasableTransactionsTests()
    {
        _mockSignService = new Mock<ISignService>();
        _mockProjectService = new Mock<IProjectService>();
        _mockNostrDecrypter = new Mock<INostrDecrypter>();
        _mockSerializer = new Mock<ISerializer>();

        _sut = new GetReleasableTransactions.GetClaimableTransactionsHandler(
            _mockSignService.Object,
            _mockProjectService.Object,
            _mockNostrDecrypter.Object,
            _mockSerializer.Object);
    }

    [Fact]
    public async Task Handle_WhenProjectNotFound_ReturnsFailure()
    {
        // Arrange
        var request = new GetReleasableTransactions.GetReleasableTransactionsRequest(
            new WalletId("wallet-1"),
            new ProjectId("nonexistent"));

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
    public async Task Handle_WhenNoInvestmentRequests_ReturnsEmptyList()
    {
        // Arrange
        var request = CreateRequest();
        SetupProject();

        // Setup LookupInvestmentRequestsAsync to immediately call onAllMessagesReceived with no items
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

        // Setup the approval and release lookups to also complete immediately
        _mockSignService
            .Setup(x => x.LookupInvestmentRequestApprovals(
                It.IsAny<string>(),
                It.IsAny<Action<string, DateTime, string>>(),
                It.IsAny<Action>()))
            .Callback<string, Action<string, DateTime, string>, Action>(
                (pubKey, onMessage, onEnd) => onEnd());

        _mockSignService
            .Setup(x => x.LookupSignedReleaseSigs(
                It.IsAny<string>(),
                It.IsAny<Action<SignServiceLookupItem>>(),
                It.IsAny<Action>()))
            .Callback<string, Action<SignServiceLookupItem>, Action>(
                (pubKey, onMessage, onEnd) => onEnd());

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Transactions.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenInvestmentRequestsFound_ReturnsTransactionList()
    {
        // Arrange
        var request = CreateRequest();
        SetupProject();

        var requestTime = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        // Setup investment request lookup
        _mockSignService
            .Setup(x => x.LookupInvestmentRequestsAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime?>(),
                It.IsAny<Action<string, string, string, DateTime>>(),
                It.IsAny<Action>()))
            .Returns<string, string?, DateTime?, Action<string, string, string, DateTime>, Action>(
                (pubKey, sender, since, onMessage, onEnd) =>
                {
                    onMessage("event-1", "investor-1", "encrypted-msg", requestTime);
                    onEnd();
                    return Task.CompletedTask;
                });

        // Setup decrypt to succeed
        _mockNostrDecrypter
            .Setup(x => x.Decrypt(It.IsAny<WalletId>(), It.IsAny<ProjectId>(), It.IsAny<DirectMessage>()))
            .ReturnsAsync(Result.Success("decrypted-json"));

        _mockSerializer
            .Setup(x => x.Deserialize<SignRecoveryRequest>(It.IsAny<string>()))
            .Returns(new SignRecoveryRequest
            {
                ProjectIdentifier = "project-1",
                InvestmentTransactionHex = "tx-hex",
                UnfundedReleaseAddress = "release-addr"
            });

        // Setup approval and release lookups
        _mockSignService
            .Setup(x => x.LookupInvestmentRequestApprovals(
                It.IsAny<string>(),
                It.IsAny<Action<string, DateTime, string>>(),
                It.IsAny<Action>()))
            .Callback<string, Action<string, DateTime, string>, Action>(
                (pubKey, onMessage, onEnd) => onEnd());

        _mockSignService
            .Setup(x => x.LookupSignedReleaseSigs(
                It.IsAny<string>(),
                It.IsAny<Action<SignServiceLookupItem>>(),
                It.IsAny<Action>()))
            .Callback<string, Action<SignServiceLookupItem>, Action>(
                (pubKey, onMessage, onEnd) => onEnd());

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Transactions.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_WhenDecryptFails_ItemHasNoSignRecoveryRequestEventId()
    {
        // Arrange
        var request = CreateRequest();
        SetupProject();

        var requestTime = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        _mockSignService
            .Setup(x => x.LookupInvestmentRequestsAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime?>(),
                It.IsAny<Action<string, string, string, DateTime>>(),
                It.IsAny<Action>()))
            .Returns<string, string?, DateTime?, Action<string, string, string, DateTime>, Action>(
                (pubKey, sender, since, onMessage, onEnd) =>
                {
                    onMessage("event-1", "investor-1", "encrypted-msg", requestTime);
                    onEnd();
                    return Task.CompletedTask;
                });

        // Decrypt fails
        _mockNostrDecrypter
            .Setup(x => x.Decrypt(It.IsAny<WalletId>(), It.IsAny<ProjectId>(), It.IsAny<DirectMessage>()))
            .ReturnsAsync(Result.Failure<string>("Decryption failed"));

        _mockSignService
            .Setup(x => x.LookupInvestmentRequestApprovals(
                It.IsAny<string>(),
                It.IsAny<Action<string, DateTime, string>>(),
                It.IsAny<Action>()))
            .Callback<string, Action<string, DateTime, string>, Action>(
                (pubKey, onMessage, onEnd) => onEnd());

        _mockSignService
            .Setup(x => x.LookupSignedReleaseSigs(
                It.IsAny<string>(),
                It.IsAny<Action<SignServiceLookupItem>>(),
                It.IsAny<Action>()))
            .Callback<string, Action<SignServiceLookupItem>, Action>(
                (pubKey, onMessage, onEnd) => onEnd());

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Item should still be in the list but without SignRecoveryRequestEventId
        var items = result.Value.Transactions.ToList();
        items.Should().HaveCount(1);
        items[0].InvestmentEventId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenDuplicateInvestorRequests_KeepsLatest()
    {
        // Arrange
        var request = CreateRequest();
        SetupProject();

        var earlyTime = new DateTime(2025, 6, 10, 12, 0, 0, DateTimeKind.Utc);
        var lateTime = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        _mockSignService
            .Setup(x => x.LookupInvestmentRequestsAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime?>(),
                It.IsAny<Action<string, string, string, DateTime>>(),
                It.IsAny<Action>()))
            .Returns<string, string?, DateTime?, Action<string, string, string, DateTime>, Action>(
                (pubKey, sender, since, onMessage, onEnd) =>
                {
                    // Same investor, two requests - earlier first
                    onMessage("event-1", "investor-1", "encrypted-msg-old", earlyTime);
                    onMessage("event-2", "investor-1", "encrypted-msg-new", lateTime);
                    onEnd();
                    return Task.CompletedTask;
                });

        _mockNostrDecrypter
            .Setup(x => x.Decrypt(It.IsAny<WalletId>(), It.IsAny<ProjectId>(), It.IsAny<DirectMessage>()))
            .ReturnsAsync(Result.Failure<string>("skip"));

        _mockSignService
            .Setup(x => x.LookupInvestmentRequestApprovals(
                It.IsAny<string>(),
                It.IsAny<Action<string, DateTime, string>>(),
                It.IsAny<Action>()))
            .Callback<string, Action<string, DateTime, string>, Action>(
                (pubKey, onMessage, onEnd) => onEnd());

        _mockSignService
            .Setup(x => x.LookupSignedReleaseSigs(
                It.IsAny<string>(),
                It.IsAny<Action<SignServiceLookupItem>>(),
                It.IsAny<Action>()))
            .Callback<string, Action<SignServiceLookupItem>, Action>(
                (pubKey, onMessage, onEnd) => onEnd());

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var items = result.Value.Transactions.ToList();
        items.Should().HaveCount(1);
        items[0].Arrived.Should().Be(lateTime);
    }

    private static GetReleasableTransactions.GetReleasableTransactionsRequest CreateRequest()
    {
        return new GetReleasableTransactions.GetReleasableTransactionsRequest(
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
}
