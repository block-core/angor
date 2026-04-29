using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor.Domain;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Funding.Shared.TransactionDrafts;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using FluentAssertions;
using MediatR;
using Moq;

namespace Angor.Sdk.Tests.Funding.Investor.Operations;

public class PublishAndStoreInvestorTransactionTests
{
    private readonly Mock<IIndexerService> _mockIndexerService;
    private readonly Mock<IPortfolioService> _mockPortfolioService;
    private readonly Mock<IMediator> _mockMediator;
    private readonly PublishAndStoreInvestorTransaction.Handler _sut;

    public PublishAndStoreInvestorTransactionTests()
    {
        _mockIndexerService = new Mock<IIndexerService>();
        _mockPortfolioService = new Mock<IPortfolioService>();
        _mockMediator = new Mock<IMediator>();

        _sut = new PublishAndStoreInvestorTransaction.Handler(
            _mockIndexerService.Object,
            _mockPortfolioService.Object,
            _mockMediator.Object,
            new Mock<Microsoft.Extensions.Logging.ILogger<PublishAndStoreInvestorTransaction.Handler>>().Object);
    }

    [Fact]
    public async Task Handle_WhenWalletIdIsNull_ReturnsFailure()
    {
        // Arrange
        var draft = CreateInvestmentDraft();
        var request = new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(
            null, new ProjectId("project-1"), draft);

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("WalletId is required");
    }

    [Fact]
    public async Task Handle_WhenWalletIdIsEmpty_ReturnsFailure()
    {
        // Arrange
        var draft = CreateInvestmentDraft();
        var request = new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(
            "", new ProjectId("project-1"), draft);

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("WalletId is required");
    }

    [Fact]
    public async Task Handle_WhenProjectIdIsNull_ReturnsFailure()
    {
        // Arrange
        var draft = CreateInvestmentDraft();
        var request = new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(
            "wallet-1", null, draft);

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("ProjectId is required");
    }

    [Fact]
    public async Task Handle_WhenSignedTxHexIsEmpty_ReturnsFailure()
    {
        // Arrange
        var draft = new InvestmentDraft("investor-key")
        {
            SignedTxHex = "",
            TransactionId = "tx-123",
            TransactionFee = new Amount(1000)
        };
        var request = new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(
            "wallet-1", new ProjectId("project-1"), draft);

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Transaction signature cannot be empty");
    }

    [Fact]
    public async Task Handle_WhenCancellationRequested_ReturnsFailure()
    {
        // Arrange
        var draft = CreateInvestmentDraft();
        var request = new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(
            "wallet-1", new ProjectId("project-1"), draft);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _sut.Handle(request, cts.Token);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cancelled");
    }

    [Fact]
    public async Task Handle_WhenPublishFails_ReturnsFailure()
    {
        // Arrange
        var draft = CreateInvestmentDraft();
        var request = new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(
            "wallet-1", new ProjectId("project-1"), draft);

        _mockIndexerService
            .Setup(x => x.PublishTransactionAsync(draft.SignedTxHex))
            .ReturnsAsync("Transaction rejected by network");

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Transaction rejected by network");
    }

    [Fact]
    public async Task Handle_WhenGetByWalletIdFails_ReturnsFailure()
    {
        // Arrange
        var draft = CreateInvestmentDraft();
        var request = new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(
            "wallet-1", new ProjectId("project-1"), draft);

        _mockIndexerService
            .Setup(x => x.PublishTransactionAsync(draft.SignedTxHex))
            .ReturnsAsync((string)null!);

        _mockPortfolioService
            .Setup(x => x.GetByWalletId("wallet-1"))
            .ReturnsAsync(Result.Failure<InvestmentRecords>("Storage unavailable"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Storage unavailable");
    }

    [Fact]
    public async Task Handle_WhenInvestmentDraftAndExistingRecord_UpdatesRecord()
    {
        // Arrange
        var draft = new InvestmentDraft("investor-key-abc")
        {
            SignedTxHex = "signed-hex",
            TransactionId = "tx-123",
            TransactionFee = new Amount(500),
            InvestedAmount = new Amount(100_000)
        };
        var request = new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(
            "wallet-1", new ProjectId("project-1"), draft);

        _mockIndexerService
            .Setup(x => x.PublishTransactionAsync("signed-hex"))
            .ReturnsAsync((string)null!);

        var existingRecord = new InvestmentRecord { ProjectIdentifier = "project-1" };
        var records = new InvestmentRecords
        {
            ProjectIdentifiers = new List<InvestmentRecord> { existingRecord }
        };
        _mockPortfolioService
            .Setup(x => x.GetByWalletId("wallet-1"))
            .ReturnsAsync(Result.Success(records));

        _mockPortfolioService
            .Setup(x => x.AddOrUpdate("wallet-1", It.IsAny<InvestmentRecord>()))
            .ReturnsAsync(Result.Success());

        _mockMediator
            .Setup(x => x.Send(It.IsAny<NotifyFounderOfInvestment.NotifyFounderOfInvestmentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new NotifyFounderOfInvestment.NotifyFounderOfInvestmentResponse(DateTime.UtcNow, "event-1")));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TransactionId.Should().Be("tx-123");
        existingRecord.InvestmentTransactionHash.Should().Be("tx-123");
        existingRecord.InvestorPubKey.Should().Be("investor-key-abc");
        existingRecord.InvestedAmountSats.Should().Be(100_000);
    }

    [Fact]
    public async Task Handle_WhenInvestmentDraftAndNoExistingRecord_CreatesNewRecord()
    {
        // Arrange
        var draft = new InvestmentDraft("investor-key-abc")
        {
            SignedTxHex = "signed-hex",
            TransactionId = "tx-123",
            TransactionFee = new Amount(500),
            InvestedAmount = new Amount(100_000)
        };
        var request = new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(
            "wallet-1", new ProjectId("project-1"), draft);

        _mockIndexerService
            .Setup(x => x.PublishTransactionAsync("signed-hex"))
            .ReturnsAsync((string)null!);

        var records = new InvestmentRecords { ProjectIdentifiers = new List<InvestmentRecord>() };
        _mockPortfolioService
            .Setup(x => x.GetByWalletId("wallet-1"))
            .ReturnsAsync(Result.Success(records));

        InvestmentRecord? savedRecord = null;
        _mockPortfolioService
            .Setup(x => x.AddOrUpdate("wallet-1", It.IsAny<InvestmentRecord>()))
            .Callback<string, InvestmentRecord>((_, r) => savedRecord = r)
            .ReturnsAsync(Result.Success());

        _mockMediator
            .Setup(x => x.Send(It.IsAny<NotifyFounderOfInvestment.NotifyFounderOfInvestmentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new NotifyFounderOfInvestment.NotifyFounderOfInvestmentResponse(DateTime.UtcNow, "event-1")));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        savedRecord.Should().NotBeNull();
        savedRecord!.ProjectIdentifier.Should().Be("project-1");
        savedRecord.InvestorPubKey.Should().Be("investor-key-abc");
        savedRecord.InvestedAmountSats.Should().Be(100_000);
    }

    [Fact]
    public async Task Handle_WhenEndOfProjectDraftAndNoRecord_ReturnsFailure()
    {
        // Arrange
        var draft = new EndOfProjectTransactionDraft
        {
            SignedTxHex = "signed-hex",
            TransactionId = "tx-eop",
            TransactionFee = new Amount(300)
        };
        var request = new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(
            "wallet-1", new ProjectId("project-1"), draft);

        _mockIndexerService
            .Setup(x => x.PublishTransactionAsync("signed-hex"))
            .ReturnsAsync((string)null!);

        var records = new InvestmentRecords { ProjectIdentifiers = new List<InvestmentRecord>() };
        _mockPortfolioService
            .Setup(x => x.GetByWalletId("wallet-1"))
            .ReturnsAsync(Result.Success(records));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Investment record not found");
    }

    [Fact]
    public async Task Handle_WhenEndOfProjectDraftAlreadyPublished_ReturnsFailure()
    {
        // Arrange
        var draft = new EndOfProjectTransactionDraft
        {
            SignedTxHex = "signed-hex",
            TransactionId = "tx-eop",
            TransactionFee = new Amount(300)
        };
        var request = new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(
            "wallet-1", new ProjectId("project-1"), draft);

        _mockIndexerService
            .Setup(x => x.PublishTransactionAsync("signed-hex"))
            .ReturnsAsync((string)null!);

        var existingRecord = new InvestmentRecord
        {
            ProjectIdentifier = "project-1",
            EndOfProjectTransactionId = "already-published"
        };
        var records = new InvestmentRecords
        {
            ProjectIdentifiers = new List<InvestmentRecord> { existingRecord }
        };
        _mockPortfolioService
            .Setup(x => x.GetByWalletId("wallet-1"))
            .ReturnsAsync(Result.Success(records));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("End of project transaction has already been published");
    }

    [Fact]
    public async Task Handle_WhenRecoveryDraftAndNoRecord_ReturnsFailure()
    {
        // Arrange
        var draft = new RecoveryTransactionDraft
        {
            SignedTxHex = "signed-hex",
            TransactionId = "tx-recovery",
            TransactionFee = new Amount(400)
        };
        var request = new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(
            "wallet-1", new ProjectId("project-1"), draft);

        _mockIndexerService
            .Setup(x => x.PublishTransactionAsync("signed-hex"))
            .ReturnsAsync((string)null!);

        var records = new InvestmentRecords { ProjectIdentifiers = new List<InvestmentRecord>() };
        _mockPortfolioService
            .Setup(x => x.GetByWalletId("wallet-1"))
            .ReturnsAsync(Result.Success(records));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Investment record not found for recovery transaction");
    }

    [Fact]
    public async Task Handle_WhenRecoveryDraftAlreadyPublished_ReturnsFailure()
    {
        // Arrange
        var draft = new RecoveryTransactionDraft
        {
            SignedTxHex = "signed-hex",
            TransactionId = "tx-recovery",
            TransactionFee = new Amount(400)
        };
        var request = new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(
            "wallet-1", new ProjectId("project-1"), draft);

        _mockIndexerService
            .Setup(x => x.PublishTransactionAsync("signed-hex"))
            .ReturnsAsync((string)null!);

        var existingRecord = new InvestmentRecord
        {
            ProjectIdentifier = "project-1",
            RecoveryTransactionId = "already-recovered"
        };
        var records = new InvestmentRecords
        {
            ProjectIdentifiers = new List<InvestmentRecord> { existingRecord }
        };
        _mockPortfolioService
            .Setup(x => x.GetByWalletId("wallet-1"))
            .ReturnsAsync(Result.Success(records));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Recovery transaction has already been published");
    }

    [Fact]
    public async Task Handle_WhenReleaseDraftWithoutRecovery_ReturnsFailure()
    {
        // Arrange
        var draft = new ReleaseTransactionDraft
        {
            SignedTxHex = "signed-hex",
            TransactionId = "tx-release",
            TransactionFee = new Amount(400)
        };
        var request = new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(
            "wallet-1", new ProjectId("project-1"), draft);

        _mockIndexerService
            .Setup(x => x.PublishTransactionAsync("signed-hex"))
            .ReturnsAsync((string)null!);

        var existingRecord = new InvestmentRecord
        {
            ProjectIdentifier = "project-1",
            RecoveryTransactionId = null
        };
        var records = new InvestmentRecords
        {
            ProjectIdentifiers = new List<InvestmentRecord> { existingRecord }
        };
        _mockPortfolioService
            .Setup(x => x.GetByWalletId("wallet-1"))
            .ReturnsAsync(Result.Success(records));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Cannot release funds without a recovery transaction");
    }

    [Fact]
    public async Task Handle_WhenReleaseDraftAlreadyPublished_ReturnsFailure()
    {
        // Arrange
        var draft = new ReleaseTransactionDraft
        {
            SignedTxHex = "signed-hex",
            TransactionId = "tx-release",
            TransactionFee = new Amount(400)
        };
        var request = new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(
            "wallet-1", new ProjectId("project-1"), draft);

        _mockIndexerService
            .Setup(x => x.PublishTransactionAsync("signed-hex"))
            .ReturnsAsync((string)null!);

        var existingRecord = new InvestmentRecord
        {
            ProjectIdentifier = "project-1",
            RecoveryTransactionId = "recovery-tx",
            RecoveryReleaseTransactionId = "already-released"
        };
        var records = new InvestmentRecords
        {
            ProjectIdentifiers = new List<InvestmentRecord> { existingRecord }
        };
        _mockPortfolioService
            .Setup(x => x.GetByWalletId("wallet-1"))
            .ReturnsAsync(Result.Success(records));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Release transaction has already been published");
    }

    [Fact]
    public async Task Handle_WhenInvestmentDraftNotificationFails_StillReturnsSuccess()
    {
        // Arrange
        var draft = new InvestmentDraft("investor-key")
        {
            SignedTxHex = "signed-hex",
            TransactionId = "tx-123",
            TransactionFee = new Amount(500),
            InvestedAmount = new Amount(50_000)
        };
        var request = new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(
            "wallet-1", new ProjectId("project-1"), draft);

        _mockIndexerService
            .Setup(x => x.PublishTransactionAsync("signed-hex"))
            .ReturnsAsync((string)null!);

        var records = new InvestmentRecords { ProjectIdentifiers = new List<InvestmentRecord>() };
        _mockPortfolioService
            .Setup(x => x.GetByWalletId("wallet-1"))
            .ReturnsAsync(Result.Success(records));

        _mockPortfolioService
            .Setup(x => x.AddOrUpdate("wallet-1", It.IsAny<InvestmentRecord>()))
            .ReturnsAsync(Result.Success());

        _mockMediator
            .Setup(x => x.Send(It.IsAny<NotifyFounderOfInvestment.NotifyFounderOfInvestmentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<NotifyFounderOfInvestment.NotifyFounderOfInvestmentResponse>("Nostr relay down"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("Notification failure should not fail the overall operation");
        result.Value.TransactionId.Should().Be("tx-123");
    }

    [Fact]
    public async Task Handle_WhenRecoveryDraftSuccessful_SetsRecoveryTransactionId()
    {
        // Arrange
        var draft = new RecoveryTransactionDraft
        {
            SignedTxHex = "signed-hex",
            TransactionId = "tx-recovery",
            TransactionFee = new Amount(400)
        };
        var request = new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(
            "wallet-1", new ProjectId("project-1"), draft);

        _mockIndexerService
            .Setup(x => x.PublishTransactionAsync("signed-hex"))
            .ReturnsAsync((string)null!);

        var existingRecord = new InvestmentRecord { ProjectIdentifier = "project-1" };
        var records = new InvestmentRecords
        {
            ProjectIdentifiers = new List<InvestmentRecord> { existingRecord }
        };
        _mockPortfolioService
            .Setup(x => x.GetByWalletId("wallet-1"))
            .ReturnsAsync(Result.Success(records));

        _mockPortfolioService
            .Setup(x => x.AddOrUpdate("wallet-1", It.IsAny<InvestmentRecord>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        existingRecord.RecoveryTransactionId.Should().Be("tx-recovery");
    }

    [Fact]
    public async Task Handle_WhenReleaseDraftSuccessful_SetsRecoveryReleaseTransactionId()
    {
        // Arrange
        var draft = new ReleaseTransactionDraft
        {
            SignedTxHex = "signed-hex",
            TransactionId = "tx-release",
            TransactionFee = new Amount(400)
        };
        var request = new PublishAndStoreInvestorTransaction.PublishAndStoreInvestorTransactionRequest(
            "wallet-1", new ProjectId("project-1"), draft);

        _mockIndexerService
            .Setup(x => x.PublishTransactionAsync("signed-hex"))
            .ReturnsAsync((string)null!);

        var existingRecord = new InvestmentRecord
        {
            ProjectIdentifier = "project-1",
            RecoveryTransactionId = "recovery-tx"
        };
        var records = new InvestmentRecords
        {
            ProjectIdentifiers = new List<InvestmentRecord> { existingRecord }
        };
        _mockPortfolioService
            .Setup(x => x.GetByWalletId("wallet-1"))
            .ReturnsAsync(Result.Success(records));

        _mockPortfolioService
            .Setup(x => x.AddOrUpdate("wallet-1", It.IsAny<InvestmentRecord>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        existingRecord.RecoveryReleaseTransactionId.Should().Be("tx-release");
    }

    private static InvestmentDraft CreateInvestmentDraft()
    {
        return new InvestmentDraft("investor-key")
        {
            SignedTxHex = "signed-hex-data",
            TransactionId = "tx-123",
            TransactionFee = new Amount(1000),
            InvestedAmount = new Amount(100_000)
        };
    }
}
