using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Shared;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;

namespace Angor.Sdk.Tests.Funding.Founder;

public class GetClaimableTransactionsTests
{
    private readonly Mock<IProjectInvestmentsService> _mockProjectInvestmentsService;
    private readonly GetClaimableTransactions.GetClaimableTransactionsHandler _sut;

    public GetClaimableTransactionsTests()
    {
        _mockProjectInvestmentsService = new Mock<IProjectInvestmentsService>();
        _sut = new GetClaimableTransactions.GetClaimableTransactionsHandler(_mockProjectInvestmentsService.Object);
    }

    [Fact]
    public async Task Handle_WhenScanFails_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var projectId = new ProjectId("project-1");
        var request = new GetClaimableTransactions.GetClaimableTransactionsRequest(walletId, projectId);

        _mockProjectInvestmentsService
            .Setup(x => x.ScanFullInvestments(projectId.Value))
            .ReturnsAsync(Result.Failure<IEnumerable<StageData>>("Indexer unavailable"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Indexer unavailable");
    }

    [Fact]
    public async Task Handle_WhenNoStages_ReturnsEmptyList()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var projectId = new ProjectId("project-1");
        var request = new GetClaimableTransactions.GetClaimableTransactionsRequest(walletId, projectId);

        _mockProjectInvestmentsService
            .Setup(x => x.ScanFullInvestments(projectId.Value))
            .ReturnsAsync(Result.Success(Enumerable.Empty<StageData>()));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Transactions.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenUnspentAndReleaseDatePassed_ReturnsUnspent()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var projectId = new ProjectId("project-1");
        var request = new GetClaimableTransactions.GetClaimableTransactionsRequest(walletId, projectId);

        var stageData = new StageData
        {
            StageIndex = 0,
            StageDate = DateTime.UtcNow.AddDays(-1), // past
            IsDynamic = false,
            Items = new List<StageDataTrx>
            {
                new StageDataTrx
                {
                    Amount = 50_000,
                    IsSpent = false,
                    InvestorPublicKey = "investor-pub-1",
                    SpentType = null
                }
            }
        };

        _mockProjectInvestmentsService
            .Setup(x => x.ScanFullInvestments(projectId.Value))
            .ReturnsAsync(Result.Success<IEnumerable<StageData>>(new[] { stageData }));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var transactions = result.Value.Transactions.ToList();
        transactions.Should().HaveCount(1);
        transactions[0].ClaimStatus.Should().Be(ClaimStatus.Unspent);
        transactions[0].StageId.Should().Be(0);
        transactions[0].StageNumber.Should().Be(1);
        transactions[0].Amount.Should().Be(new Amount(50_000));
        transactions[0].InvestorAddress.Should().Be("investor-pub-1");
        transactions[0].DynamicReleaseDate.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenReleaseDateInFuture_ReturnsLocked()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var projectId = new ProjectId("project-1");
        var request = new GetClaimableTransactions.GetClaimableTransactionsRequest(walletId, projectId);

        var futureDate = DateTime.UtcNow.AddDays(30);
        var stageData = new StageData
        {
            StageIndex = 1,
            StageDate = futureDate,
            IsDynamic = false,
            Items = new List<StageDataTrx>
            {
                new StageDataTrx
                {
                    Amount = 100_000,
                    IsSpent = false,
                    InvestorPublicKey = "investor-pub-1",
                    SpentType = null
                }
            }
        };

        _mockProjectInvestmentsService
            .Setup(x => x.ScanFullInvestments(projectId.Value))
            .ReturnsAsync(Result.Success<IEnumerable<StageData>>(new[] { stageData }));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var transactions = result.Value.Transactions.ToList();
        transactions.Should().HaveCount(1);
        transactions[0].ClaimStatus.Should().Be(ClaimStatus.Locked);
        transactions[0].StageNumber.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WhenSpentByFounder_ReturnsSpentByFounder()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var projectId = new ProjectId("project-1");
        var request = new GetClaimableTransactions.GetClaimableTransactionsRequest(walletId, projectId);

        var stageData = new StageData
        {
            StageIndex = 0,
            StageDate = DateTime.UtcNow.AddDays(-10),
            IsDynamic = false,
            Items = new List<StageDataTrx>
            {
                new StageDataTrx
                {
                    Amount = 50_000,
                    IsSpent = true,
                    InvestorPublicKey = "investor-pub-1",
                    ProjectScriptType = new ProjectScriptType { ScriptType = ProjectScriptTypeEnum.Founder }
                }
            }
        };

        _mockProjectInvestmentsService
            .Setup(x => x.ScanFullInvestments(projectId.Value))
            .ReturnsAsync(Result.Success<IEnumerable<StageData>>(new[] { stageData }));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Transactions.First().ClaimStatus.Should().Be(ClaimStatus.SpentByFounder);
    }

    [Fact]
    public async Task Handle_WhenSpentByInvestorWithPenalty_ReturnsWithdrawByInvestor()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var projectId = new ProjectId("project-1");
        var request = new GetClaimableTransactions.GetClaimableTransactionsRequest(walletId, projectId);

        var stageData = new StageData
        {
            StageIndex = 0,
            StageDate = DateTime.UtcNow.AddDays(-5),
            IsDynamic = false,
            Items = new List<StageDataTrx>
            {
                new StageDataTrx
                {
                    Amount = 75_000,
                    IsSpent = true,
                    InvestorPublicKey = "investor-pub-1",
                    ProjectScriptType = new ProjectScriptType { ScriptType = ProjectScriptTypeEnum.InvestorWithPenalty }
                }
            }
        };

        _mockProjectInvestmentsService
            .Setup(x => x.ScanFullInvestments(projectId.Value))
            .ReturnsAsync(Result.Success<IEnumerable<StageData>>(new[] { stageData }));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Transactions.First().ClaimStatus.Should().Be(ClaimStatus.WithdrawByInvestor);
    }

    [Fact]
    public async Task Handle_WhenSpentByEndOfProject_ReturnsWithdrawByInvestor()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var projectId = new ProjectId("project-1");
        var request = new GetClaimableTransactions.GetClaimableTransactionsRequest(walletId, projectId);

        var stageData = new StageData
        {
            StageIndex = 0,
            StageDate = DateTime.UtcNow.AddDays(-5),
            IsDynamic = false,
            Items = new List<StageDataTrx>
            {
                new StageDataTrx
                {
                    Amount = 75_000,
                    IsSpent = true,
                    InvestorPublicKey = "investor-pub-1",
                    ProjectScriptType = new ProjectScriptType { ScriptType = ProjectScriptTypeEnum.EndOfProject }
                }
            }
        };

        _mockProjectInvestmentsService
            .Setup(x => x.ScanFullInvestments(projectId.Value))
            .ReturnsAsync(Result.Success<IEnumerable<StageData>>(new[] { stageData }));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Transactions.First().ClaimStatus.Should().Be(ClaimStatus.WithdrawByInvestor);
    }

    [Fact]
    public async Task Handle_WhenSpentWithUnknownScriptType_ReturnsPending()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var projectId = new ProjectId("project-1");
        var request = new GetClaimableTransactions.GetClaimableTransactionsRequest(walletId, projectId);

        var stageData = new StageData
        {
            StageIndex = 0,
            StageDate = DateTime.UtcNow.AddDays(-5),
            IsDynamic = false,
            Items = new List<StageDataTrx>
            {
                new StageDataTrx
                {
                    Amount = 50_000,
                    IsSpent = true,
                    InvestorPublicKey = "investor-pub-1",
                    ProjectScriptType = new ProjectScriptType { ScriptType = ProjectScriptTypeEnum.Unknown }
                }
            }
        };

        _mockProjectInvestmentsService
            .Setup(x => x.ScanFullInvestments(projectId.Value))
            .ReturnsAsync(Result.Success<IEnumerable<StageData>>(new[] { stageData }));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Transactions.First().ClaimStatus.Should().Be(ClaimStatus.Pending);
    }

    [Fact]
    public async Task Handle_WhenSpentWithNoScriptType_FallsBackToSpentType()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var projectId = new ProjectId("project-1");
        var request = new GetClaimableTransactions.GetClaimableTransactionsRequest(walletId, projectId);

        var stageData = new StageData
        {
            StageIndex = 0,
            StageDate = DateTime.UtcNow.AddDays(-5),
            IsDynamic = false,
            Items = new List<StageDataTrx>
            {
                new StageDataTrx
                {
                    Amount = 50_000,
                    IsSpent = true,
                    InvestorPublicKey = "investor-pub-1",
                    ProjectScriptType = null,
                    SpentType = "founder"
                }
            }
        };

        _mockProjectInvestmentsService
            .Setup(x => x.ScanFullInvestments(projectId.Value))
            .ReturnsAsync(Result.Success<IEnumerable<StageData>>(new[] { stageData }));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Transactions.First().ClaimStatus.Should().Be(ClaimStatus.SpentByFounder);
    }

    [Fact]
    public async Task Handle_WhenSpentWithSpentTypeInvestor_ReturnsWithdrawByInvestor()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var projectId = new ProjectId("project-1");
        var request = new GetClaimableTransactions.GetClaimableTransactionsRequest(walletId, projectId);

        var stageData = new StageData
        {
            StageIndex = 0,
            StageDate = DateTime.UtcNow.AddDays(-5),
            IsDynamic = false,
            Items = new List<StageDataTrx>
            {
                new StageDataTrx
                {
                    Amount = 50_000,
                    IsSpent = true,
                    InvestorPublicKey = "investor-pub-1",
                    ProjectScriptType = null,
                    SpentType = "investor"
                }
            }
        };

        _mockProjectInvestmentsService
            .Setup(x => x.ScanFullInvestments(projectId.Value))
            .ReturnsAsync(Result.Success<IEnumerable<StageData>>(new[] { stageData }));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Transactions.First().ClaimStatus.Should().Be(ClaimStatus.WithdrawByInvestor);
    }

    [Fact]
    public async Task Handle_WhenDynamicStage_SetsDynamicReleaseDate()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var projectId = new ProjectId("project-1");
        var request = new GetClaimableTransactions.GetClaimableTransactionsRequest(walletId, projectId);

        var dynamicDate = new DateTime(2025, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        var stageData = new StageData
        {
            StageIndex = 2,
            StageDate = dynamicDate,
            IsDynamic = true,
            Items = new List<StageDataTrx>
            {
                new StageDataTrx
                {
                    Amount = 100_000,
                    IsSpent = false,
                    InvestorPublicKey = "investor-pub-1"
                }
            }
        };

        _mockProjectInvestmentsService
            .Setup(x => x.ScanFullInvestments(projectId.Value))
            .ReturnsAsync(Result.Success<IEnumerable<StageData>>(new[] { stageData }));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var tx = result.Value.Transactions.First();
        tx.DynamicReleaseDate.Should().Be(dynamicDate);
    }

    [Fact]
    public async Task Handle_WhenNonDynamicStage_DynamicReleaseDateIsNull()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var projectId = new ProjectId("project-1");
        var request = new GetClaimableTransactions.GetClaimableTransactionsRequest(walletId, projectId);

        var stageData = new StageData
        {
            StageIndex = 0,
            StageDate = DateTime.UtcNow.AddDays(-1),
            IsDynamic = false,
            Items = new List<StageDataTrx>
            {
                new StageDataTrx
                {
                    Amount = 100_000,
                    IsSpent = false,
                    InvestorPublicKey = "investor-pub-1"
                }
            }
        };

        _mockProjectInvestmentsService
            .Setup(x => x.ScanFullInvestments(projectId.Value))
            .ReturnsAsync(Result.Success<IEnumerable<StageData>>(new[] { stageData }));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Transactions.First().DynamicReleaseDate.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithMultipleStagesAndItems_FlattensCorrectly()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var projectId = new ProjectId("project-1");
        var request = new GetClaimableTransactions.GetClaimableTransactionsRequest(walletId, projectId);

        var stages = new[]
        {
            new StageData
            {
                StageIndex = 0,
                StageDate = DateTime.UtcNow.AddDays(-10),
                IsDynamic = false,
                Items = new List<StageDataTrx>
                {
                    new StageDataTrx { Amount = 10_000, IsSpent = false, InvestorPublicKey = "inv-1" },
                    new StageDataTrx { Amount = 20_000, IsSpent = false, InvestorPublicKey = "inv-2" }
                }
            },
            new StageData
            {
                StageIndex = 1,
                StageDate = DateTime.UtcNow.AddDays(30),
                IsDynamic = false,
                Items = new List<StageDataTrx>
                {
                    new StageDataTrx { Amount = 30_000, IsSpent = false, InvestorPublicKey = "inv-1" }
                }
            }
        };

        _mockProjectInvestmentsService
            .Setup(x => x.ScanFullInvestments(projectId.Value))
            .ReturnsAsync(Result.Success<IEnumerable<StageData>>(stages));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var transactions = result.Value.Transactions.ToList();
        transactions.Should().HaveCount(3);
        transactions.Count(t => t.ClaimStatus == ClaimStatus.Unspent).Should().Be(2);
        transactions.Count(t => t.ClaimStatus == ClaimStatus.Locked).Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenSpentWithFutureReleaseDate_StillShowsAsSpent()
    {
        // Arrange — spent status should override the date check
        var walletId = new WalletId("wallet-1");
        var projectId = new ProjectId("project-1");
        var request = new GetClaimableTransactions.GetClaimableTransactionsRequest(walletId, projectId);

        var stageData = new StageData
        {
            StageIndex = 0,
            StageDate = DateTime.UtcNow.AddDays(30), // future, but already spent
            IsDynamic = false,
            Items = new List<StageDataTrx>
            {
                new StageDataTrx
                {
                    Amount = 50_000,
                    IsSpent = true,
                    InvestorPublicKey = "investor-pub-1",
                    ProjectScriptType = new ProjectScriptType { ScriptType = ProjectScriptTypeEnum.Founder }
                }
            }
        };

        _mockProjectInvestmentsService
            .Setup(x => x.ScanFullInvestments(projectId.Value))
            .ReturnsAsync(Result.Success<IEnumerable<StageData>>(new[] { stageData }));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Transactions.First().ClaimStatus.Should().Be(ClaimStatus.SpentByFounder,
            "Spent status should take priority over future release date");
    }
}
