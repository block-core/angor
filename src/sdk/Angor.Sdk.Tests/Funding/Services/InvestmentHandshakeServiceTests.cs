using Angor.Data.Documents.Interfaces;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using Serilog;
using Xunit;

namespace Angor.Sdk.Tests.Funding.Services;

public class InvestmentHandshakeServiceTests
{
    private const string ProjectNostrPubKey = "aaaa1111aaaa1111aaaa1111aaaa1111aaaa1111aaaa1111aaaa1111aaaa1111";
    private const string InvestorNostrPubKey = "bbbb2222bbbb2222bbbb2222bbbb2222bbbb2222bbbb2222bbbb2222bbbb2222";
    private const string AttackerNostrPubKey = "cccc3333cccc3333cccc3333cccc3333cccc3333cccc3333cccc3333cccc3333";
    private const string RequestEventId = "request-event-id-1";

    private readonly Mock<ISignService> _mockSignService = new();
    private readonly Mock<INostrDecrypter> _mockNostrDecrypter = new();
    private readonly Mock<ISerializer> _mockSerializer = new();
    private readonly Mock<ILogger> _mockLogger = new();
    private readonly Mock<IGenericDocumentCollection<InvestmentHandshake>> _mockCollection = new();

    private readonly WalletId _walletId = new("wallet-1");
    private readonly ProjectId _projectId = new("angor-project-1");

    private InvestmentHandshakeService CreateSut()
    {
        _mockCollection
            .Setup(c => c.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<InvestmentHandshake, bool>>>()))
            .ReturnsAsync(Result.Success(Enumerable.Empty<InvestmentHandshake>()));
        _mockCollection
            .Setup(c => c.UpsertAsync(It.IsAny<Func<InvestmentHandshake, string>>(), It.IsAny<InvestmentHandshake>()))
            .ReturnsAsync(Result.Success(true));

        _mockNostrDecrypter
            .Setup(d => d.Decrypt(It.IsAny<WalletId>(), It.IsAny<ProjectId>(), It.IsAny<DirectMessage>()))
            .ReturnsAsync(Result.Failure<string>("not decryptable in test"));

        return new InvestmentHandshakeService(
            _mockSignService.Object,
            _mockNostrDecrypter.Object,
            _mockSerializer.Object,
            _mockLogger.Object,
            _mockCollection.Object);
    }

    private void SetupNostrMessages(params (InvestmentMessageType Type, string Id, string PubKey)[] messages)
    {
        _mockSignService
            .Setup(s => s.LookupAllInvestmentMessagesAsync(
                ProjectNostrPubKey,
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<Action<InvestmentMessageType, string, string, string, DateTime>>(),
                It.IsAny<Action>()))
            .Callback((string _, string? _, DateTime? _,
                Action<InvestmentMessageType, string, string, string, DateTime> onMessage,
                Action onAllMessagesReceived) =>
            {
                foreach (var (type, id, pubKey) in messages)
                {
                    onMessage(type, id, pubKey, "encrypted-content", DateTime.UtcNow);
                }

                onAllMessagesReceived();
            })
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task SyncHandshakesFromNostr_WhenApprovalAuthoredByProjectKey_MarksRequestApproved()
    {
        // Arrange
        SetupNostrMessages(
            (InvestmentMessageType.Request, RequestEventId, InvestorNostrPubKey),
            (InvestmentMessageType.Approval, RequestEventId, ProjectNostrPubKey));
        var sut = CreateSut();

        // Act
        var result = await sut.SyncHandshakesFromNostrAsync(_walletId, _projectId, ProjectNostrPubKey);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var handshake = result.Value.Should().ContainSingle().Subject;
        handshake.Status.Should().Be(InvestmentRequestStatus.Approved);
        handshake.ApprovalEventId.Should().Be(RequestEventId);
    }

    [Fact]
    public async Task SyncHandshakesFromNostr_WhenApprovalAuthoredByOtherKey_IgnoresSpoofedApproval()
    {
        // Arrange
        SetupNostrMessages(
            (InvestmentMessageType.Request, RequestEventId, InvestorNostrPubKey),
            (InvestmentMessageType.Approval, RequestEventId, AttackerNostrPubKey));
        var sut = CreateSut();

        // Act
        var result = await sut.SyncHandshakesFromNostrAsync(_walletId, _projectId, ProjectNostrPubKey);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var handshake = result.Value.Should().ContainSingle().Subject;
        handshake.Status.Should().Be(InvestmentRequestStatus.Pending);
        handshake.ApprovalEventId.Should().BeNull();
    }

    [Fact]
    public async Task SyncHandshakesFromNostr_WhenSpoofedApprovalTargetsExistingHandshake_DoesNotUpdateStatus()
    {
        // Arrange
        SetupNostrMessages(
            (InvestmentMessageType.Request, RequestEventId, InvestorNostrPubKey),
            (InvestmentMessageType.Approval, RequestEventId, AttackerNostrPubKey));
        var sut = CreateSut();

        var existing = new InvestmentHandshake
        {
            Id = "existing-id",
            WalletId = _walletId.Value,
            ProjectId = _projectId.Value,
            RequestEventId = RequestEventId,
            InvestorNostrPubKey = InvestorNostrPubKey,
            Status = InvestmentRequestStatus.Pending
        };
        _mockCollection
            .Setup(c => c.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<InvestmentHandshake, bool>>>()))
            .ReturnsAsync(Result.Success<IEnumerable<InvestmentHandshake>>(new[] { existing }));

        // Act
        var result = await sut.SyncHandshakesFromNostrAsync(_walletId, _projectId, ProjectNostrPubKey);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty(); // nothing changed, nothing upserted
        existing.Status.Should().Be(InvestmentRequestStatus.Pending);
        existing.ApprovalEventId.Should().BeNull();
    }
}
