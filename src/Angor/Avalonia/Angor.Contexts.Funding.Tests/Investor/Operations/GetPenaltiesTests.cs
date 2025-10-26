using Angor.Contexts.Funding.Investor.Domain;
using Angor.Contexts.Funding.Investor.Operations;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Impl;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using Angor.Contexts.Funding.Services;
using Angor.Shared.Models;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using Moq;

namespace Angor.Contexts.Funding.Tests.Investor.Operations;

public class GetPenaltiesTests
{
    private readonly Mock<IPortfolioService> _mockInvestmentService;
    private readonly Mock<IIndexerService> _mockIndexerService;
    private readonly Mock<IRelayService> _mockRelayService;
    private readonly Mock<ITransactionService> _mockTransactionService;
    private readonly Mock<IProjectInvestmentsService> _mockProjectInvestmentsService;
    private readonly GetPenalties.GetPenaltiesHandler _handler;

    public GetPenaltiesTests()
    {
        _mockInvestmentService = new Mock<IPortfolioService>();
        _mockIndexerService = new Mock<IIndexerService>();
        _mockRelayService = new Mock<IRelayService>();
        _mockTransactionService = new Mock<ITransactionService>();
        _mockProjectInvestmentsService = new Mock<IProjectInvestmentsService>();
        
        _handler = new GetPenalties.GetPenaltiesHandler(
            _mockInvestmentService.Object,
            _mockIndexerService.Object,
            _mockRelayService.Object,
            _mockTransactionService.Object,
            _mockProjectInvestmentsService.Object);
    }

    [Fact]
    public async Task Handle_WhenInvestmentRepositoryFails_ShouldReturnFailure()
    {
        // Arrange
        var walletId = Guid.NewGuid();
        var request = new GetPenalties.GetPenaltiesRequest(walletId);
        var expectedError = "Repository error";

        _mockInvestmentService
            .Setup(x => x.GetByWalletId(walletId))
            .ReturnsAsync(Result.Failure<InvestmentRecords>(expectedError));

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(expectedError, result.Error);
    }

    [Fact]
    public async Task Handle_WhenFetchInvestedProjectsFails_ShouldReturnFailure()
    {
        // Arrange
        var walletId = Guid.NewGuid();
        var request = new GetPenalties.GetPenaltiesRequest(walletId);
        var investment = new InvestmentRecords();

        _mockInvestmentService
            .Setup(x => x.GetByWalletId(walletId))
            .ReturnsAsync(Result.Success(investment));

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task Handle_WhenSuccessful_ShouldReturnPenaltiesDto()
    {
        // Arrange
        var walletId = Guid.NewGuid();
        var request = new GetPenalties.GetPenaltiesRequest(walletId);
        var projectIdentifier = "test-project-id";
        var investorPubKey = "test-investor-pubkey";
        var nostrEventId = "test-event-id";
        var transactionId = "test-transaction-id";
        var transactionHex = "test-transaction-hex";

        var investment = new InvestmentRecords
        {
            ProjectIdentifiers = [new InvestmentRecord
            {
                ProjectIdentifier = projectIdentifier,
                InvestorPubKey = investorPubKey,
                InvestmentTransactionHash = transactionId,
                InvestmentTransactionHex = transactionHex,
                UnfundedReleaseAddress = null // Assuming this is not used in the test
            }]
        };
        var projectInvestment = new ProjectInvestment
        {
            TransactionId = transactionId,
            TotalAmount = 1000000
        };

        _mockInvestmentService
            .Setup(x => x.GetByWalletId(walletId))
            .ReturnsAsync(Result.Success(investment));

        _mockIndexerService
            .Setup(x => x.GetInvestmentAsync(projectIdentifier, investorPubKey))
            .ReturnsAsync(projectInvestment);

        _mockIndexerService
            .Setup(x => x.GetProjectByIdAsync(projectIdentifier))
            .ReturnsAsync(new ProjectIndexerData { TrxId = transactionId, NostrEventId = nostrEventId });

        _mockProjectInvestmentsService.Setup(x =>
                x.ScanInvestmentSpends(It.Is<ProjectInfo>(p => p.ProjectIdentifier == projectIdentifier),
                    transactionId))
            .ReturnsAsync(Result.Success(new InvestmentSpendingLookup
            {
                AmountInRecovery = 500000,
                RecoveryTransactionId = "recovery-tx-id",
                EndOfProjectTransactionId = "end-of-project-tx-id",
                RecoveryReleaseTransactionId = "recovery-release-tx-id",
                UnfundedReleaseTransactionId = "unfunded-release-tx-id",
                TransactionId = projectInvestment.TransactionId,
                ProjectIdentifier = projectIdentifier
            }));
        
        _mockRelayService
            .Setup(x => x.LookupProjectsInfoByEventIds(
                It.IsAny<Action<ProjectInfo>>(),
                It.IsAny<Action>(),
                It.IsAny<string[]>()))
            .Callback<Action<ProjectInfo>, Action, string[]>((onNext, onCompleted, eventIds) =>
            {
                onNext(new ProjectInfo
                {
                    ProjectIdentifier = projectIdentifier,
                    PenaltyDays = 30,
                    Stages = []
                });
                onCompleted();
            });

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var penaltyDto = result.Value.FirstOrDefault();
        Assert.NotNull(penaltyDto);
        Assert.Equal(projectIdentifier, penaltyDto.ProjectIdentifier);
        Assert.Equal(investorPubKey, penaltyDto.InvestorPubKey);
    }

    [Fact]
    public async Task FetchInvestedProjects_WhenInvestmentRepositoryFails_ShouldReturnFailure()
    {
        // Arrange
        var walletId = Guid.NewGuid();
        var expectedError = "Repository error";

        _mockInvestmentService
            .Setup(x => x.GetByWalletId(walletId))
            .ReturnsAsync(Result.Failure<InvestmentRecords>(expectedError));

        // Act
        var result = await _handler.FetchInvestedProjects(walletId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(expectedError, result.Error);
    }

    [Fact]
    public async Task FetchInvestedProjects_WhenNoInvestments_ShouldReturnEmptyList()
    {
        // Arrange
        var walletId = Guid.NewGuid();
        var investment = new InvestmentRecords();

        _mockInvestmentService
            .Setup(x => x.GetByWalletId(walletId))
            .ReturnsAsync(Result.Success(investment));

        // Act
        var result = await _handler.FetchInvestedProjects(walletId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task RefreshPenalties_WhenSuccessful_ShouldUpdatePenaltyData()
    {
        // Arrange
        var lookupInvestment = new LookupInvestment
        {
            ProjectIdentifier = "test-project",
            InvestorPubKey = "test-pubkey",
            RecoveryTransactionId = "recovery-tx-id",
            ProjectInfo = new ProjectInfo { PenaltyDays = 30 }
        };

        var transactionInfo = new QueryTransaction
        {
            Timestamp = DateTimeOffset.UtcNow.AddDays(-10).ToUnixTimeSeconds(),
            Outputs = new List<QueryTransactionOutput>
            {
                new (){ ScriptPubKey = "0020" + new string('0', 64), Balance = 500000 },
                new (){ ScriptPubKey = "76a914" + new string('0', 40) + "88ac", Balance = 100000 }
            }
        };

        _mockTransactionService
            .Setup(x => x.GetTransactionInfoByIdAsync("recovery-tx-id"))
            .ReturnsAsync(transactionInfo);

        var penaltyProjects = new List<LookupInvestment> { lookupInvestment };

        // Act
        var result = await _handler.RefreshPenalties(penaltyProjects);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(500000, lookupInvestment.TotalAmountSats);
        Assert.Equal(20, lookupInvestment.DaysLeftForPenalty);
        Assert.False(lookupInvestment.IsExpired);
    }

    [Fact]
    public async Task RefreshPenalties_WhenExpired_ShouldSetIsExpiredTrue()
    {
        // Arrange
        var lookupInvestment = new LookupInvestment
        {
            ProjectIdentifier = "test-project",
            InvestorPubKey = "test-pubkey",
            RecoveryTransactionId = "recovery-tx-id",
            ProjectInfo = new ProjectInfo { PenaltyDays = 10 }
        };

        var transactionInfo = new QueryTransaction
        {
            Timestamp = DateTimeOffset.UtcNow.AddDays(-20).ToUnixTimeSeconds(),
            Outputs = new List<QueryTransactionOutput>
            {
                new (){ ScriptPubKey = "0020" + new string('0', 64), Balance = 500000 }
            }
        };

        _mockTransactionService
            .Setup(x => x.GetTransactionInfoByIdAsync("recovery-tx-id"))
            .ReturnsAsync(transactionInfo);

        var penaltyProjects = new List<LookupInvestment> { lookupInvestment };

        // Act
        var result = await _handler.RefreshPenalties(penaltyProjects);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(lookupInvestment.IsExpired);
        Assert.True(lookupInvestment.DaysLeftForPenalty <= 0);
    }

    [Fact]
    public async Task RefreshPenalties_WhenExceptionOccurs_ShouldReturnFailure()
    {
        // Arrange
        var lookupInvestment = new LookupInvestment
        {
            ProjectIdentifier = "test-project",
            InvestorPubKey = "test-pubkey",
            RecoveryTransactionId = "recovery-tx-id",
            ProjectInfo = new ProjectInfo { PenaltyDays = 30 }
        };

        _mockTransactionService
            .Setup(x => x.GetTransactionInfoByIdAsync("recovery-tx-id"))
            .ThrowsAsync(new Exception("Test exception"));

        var penaltyProjects = new List<LookupInvestment> { lookupInvestment };

        // Act
        var result = await _handler.RefreshPenalties(penaltyProjects);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Test exception", result.Error);
    }

    [Fact]
    public void GetPenaltiesRequest_ShouldHaveCorrectWalletId()
    {
        // Arrange
        var walletId = Guid.NewGuid();

        // Act
        var request = new GetPenalties.GetPenaltiesRequest(walletId);

        // Assert
        Assert.Equal(walletId, request.WalletId);
    }
}