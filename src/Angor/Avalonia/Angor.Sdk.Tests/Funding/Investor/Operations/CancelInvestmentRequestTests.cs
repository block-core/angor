using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor.Domain;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Tests.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using MediatR;
using Moq;
using Xunit;

namespace Angor.Sdk.Tests.Funding.Investor.Operations;

public class CancelInvestmentRequestTests : IClassFixture<TestNetworkFixture>
{
    private readonly TestNetworkFixture _fixture;
    private readonly Mock<IPortfolioService> _mockPortfolioService;
    private readonly Mock<IAngorIndexerService> _mockAngorIndexerService;
    private readonly Mock<IWalletAccountBalanceService> _mockWalletAccountBalanceService;
    private readonly Mock<IMediator> _mockMediator;
    private readonly CancelInvestmentRequest.CancelInvestmentRequestHandler _sut;

    public CancelInvestmentRequestTests(TestNetworkFixture fixture)
    {
        _fixture = fixture;
        _mockPortfolioService = new Mock<IPortfolioService>();
        _mockAngorIndexerService = new Mock<IAngorIndexerService>();
        _mockWalletAccountBalanceService = new Mock<IWalletAccountBalanceService>();
        _mockMediator = new Mock<IMediator>();

        _sut = new CancelInvestmentRequest.CancelInvestmentRequestHandler(
            _mockPortfolioService.Object,
            _mockAngorIndexerService.Object,
            _fixture.NetworkConfiguration,
            _mockWalletAccountBalanceService.Object,
            _mockMediator.Object);
    }

    [Fact]
    public async Task Handle_WhenRecordExistsWithHashAndNotPublished_DeletesRecord()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var projectId = new ProjectId("test-project-id");
        var txHash = "abc123txhash";
        var investorPubKey = "somepubkey";

        var records = new InvestmentRecords
        {
            ProjectIdentifiers = new List<InvestmentRecord>
            {
                new InvestmentRecord
                {
                    ProjectIdentifier = projectId.Value,
                    InvestmentTransactionHash = txHash,
                    InvestorPubKey = investorPubKey
                }
            }
        };

        _mockPortfolioService
            .Setup(x => x.GetByWalletId(walletId.Value))
            .ReturnsAsync(Result.Success(records));

        _mockPortfolioService
            .Setup(x => x.RemoveInvestmentRecordAsync(walletId.Value, It.IsAny<InvestmentRecord>()))
            .ReturnsAsync(Result.Success());

        // Transaction not yet on chain
        _mockAngorIndexerService
            .Setup(x => x.GetInvestmentAsync(projectId.Value, investorPubKey))
            .ReturnsAsync((ProjectInvestment?)null);

        var request = new CancelInvestmentRequest.CancelInvestmentRequestRequest(walletId, projectId, txHash);

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        _mockPortfolioService.Verify(
            x => x.RemoveInvestmentRecordAsync(walletId.Value, It.Is<InvestmentRecord>(r => r.ProjectIdentifier == projectId.Value)),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTransactionIsPublished_ReturnsFailure()
    {
        // Arrange: the transaction has been confirmed on-chain — cancellation must be rejected
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var projectId = new ProjectId("test-project-id");
        var txHash = "abc123txhash";
        var investorPubKey = "somepubkey";

        var records = new InvestmentRecords
        {
            ProjectIdentifiers = new List<InvestmentRecord>
            {
                new InvestmentRecord
                {
                    ProjectIdentifier = projectId.Value,
                    InvestmentTransactionHash = txHash,
                    InvestorPubKey = investorPubKey
                }
            }
        };

        _mockPortfolioService
            .Setup(x => x.GetByWalletId(walletId.Value))
            .ReturnsAsync(Result.Success(records));

        // Indexer confirms the investment is on chain
        _mockAngorIndexerService
            .Setup(x => x.GetInvestmentAsync(projectId.Value, investorPubKey))
            .ReturnsAsync(new ProjectInvestment { TransactionId = txHash, InvestorPublicKey = investorPubKey, TotalAmount = 100_000 });

        var request = new CancelInvestmentRequest.CancelInvestmentRequestRequest(walletId, projectId, txHash);

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("published to the blockchain", result.Error);
        _mockPortfolioService.Verify(
            x => x.RemoveInvestmentRecordAsync(It.IsAny<string>(), It.IsAny<InvestmentRecord>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenHashDoesNotMatch_ReturnsFailure()
    {
        // Arrange: the caller provides a different InvestmentId than what is stored
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var projectId = new ProjectId("test-project-id");

        var records = new InvestmentRecords
        {
            ProjectIdentifiers = new List<InvestmentRecord>
            {
                new InvestmentRecord
                {
                    ProjectIdentifier = projectId.Value,
                    InvestmentTransactionHash = "correct-hash",
                    InvestorPubKey = "somepubkey"
                }
            }
        };

        _mockPortfolioService
            .Setup(x => x.GetByWalletId(walletId.Value))
            .ReturnsAsync(Result.Success(records));

        var request = new CancelInvestmentRequest.CancelInvestmentRequestRequest(walletId, projectId, "wrong-hash");

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("Investment record not found", result.Error);
    }

    [Fact]
    public async Task Handle_WhenNoRecordExists_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var projectId = new ProjectId("nonexistent-project");

        var records = new InvestmentRecords
        {
            ProjectIdentifiers = new List<InvestmentRecord>()
        };

        _mockPortfolioService
            .Setup(x => x.GetByWalletId(walletId.Value))
            .ReturnsAsync(Result.Success(records));

        var request = new CancelInvestmentRequest.CancelInvestmentRequestRequest(walletId, projectId, "any-id");

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("Investment record not found", result.Error);
    }

    [Fact]
    public async Task Handle_WhenRecordHasRequestEventId_NotifiesFounderOfCancellation()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var projectId = new ProjectId("test-project-id");
        var txHash = "abc123txhash";
        var eventId = "nostr-event-id-123";
        var investorPubKey = "somepubkey";

        var records = new InvestmentRecords
        {
            ProjectIdentifiers = new List<InvestmentRecord>
            {
                new InvestmentRecord
                {
                    ProjectIdentifier = projectId.Value,
                    InvestmentTransactionHash = txHash,
                    RequestEventId = eventId,
                    InvestorPubKey = investorPubKey
                }
            }
        };

        _mockPortfolioService
            .Setup(x => x.GetByWalletId(walletId.Value))
            .ReturnsAsync(Result.Success(records));

        _mockPortfolioService
            .Setup(x => x.RemoveInvestmentRecordAsync(walletId.Value, It.IsAny<InvestmentRecord>()))
            .ReturnsAsync(Result.Success());

        // Transaction not yet on chain
        _mockAngorIndexerService
            .Setup(x => x.GetInvestmentAsync(projectId.Value, investorPubKey))
            .ReturnsAsync((ProjectInvestment?)null);

        _mockMediator
            .Setup(x => x.Send(It.IsAny<NotifyFounderOfCancellation.NotifyFounderOfCancellationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new NotifyFounderOfCancellation.NotifyFounderOfCancellationResponse(DateTime.UtcNow, "event-id")));

        var request = new CancelInvestmentRequest.CancelInvestmentRequestRequest(walletId, projectId, txHash);

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        _mockMediator.Verify(
            x => x.Send(
                It.Is<NotifyFounderOfCancellation.NotifyFounderOfCancellationRequest>(r =>
                    r.RequestEventId == eventId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
