using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Tests.Shared;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using Angor.Shared.Services;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using Xunit;

namespace Angor.Sdk.Tests.Funding.Projects;

/// <summary>
/// Unit tests for ProjectInvestmentsService.
/// Tests the scanning of investments and spending detection.
/// </summary>
public class ProjectInvestmentsServiceTests : IClassFixture<TestNetworkFixture>
{
    private readonly TestNetworkFixture _fixture;
    private readonly Mock<IProjectService> _mockProjectService;
    private readonly Mock<IAngorIndexerService> _mockAngorIndexerService;
    private readonly Mock<ITransactionService> _mockTransactionService;
    private readonly ProjectInvestmentsService _sut;

    public ProjectInvestmentsServiceTests(TestNetworkFixture fixture)
    {
        _fixture = fixture;
        _mockProjectService = new Mock<IProjectService>();
        _mockAngorIndexerService = new Mock<IAngorIndexerService>();
        _mockTransactionService = new Mock<ITransactionService>();

        _sut = new ProjectInvestmentsService(
            _mockProjectService.Object,
            _fixture.NetworkConfiguration,
            _mockAngorIndexerService.Object,
            _fixture.InvestorTransactionActions,
            _mockTransactionService.Object);
    }

    #region ScanFullInvestments Tests

    [Fact]
    public async Task ScanFullInvestments_WhenProjectNotFound_ReturnsFailure()
    {
        // Arrange
        var projectId = "test-project-id";
        
        _mockProjectService
            .Setup(x => x.GetAsync(It.Is<ProjectId>(p => p.Value == projectId)))
            .ReturnsAsync(Result.Failure<Project>("Project not found"));

        // Act
        var result = await _sut.ScanFullInvestments(projectId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to retrieve project data");
    }

    [Fact]
    public async Task ScanFullInvestments_WhenNoInvestments_ReturnsEmptyList()
    {
        // Arrange
        var project = TestDataBuilder.CreateProject().Build();
        
        _mockProjectService
            .Setup(x => x.GetAsync(It.IsAny<ProjectId>()))
            .ReturnsAsync(Result.Success(project));
        
        _mockAngorIndexerService
            .Setup(x => x.GetInvestmentsAsync(project.Id.Value))
            .ReturnsAsync(new List<ProjectInvestment>());

        // Act
        var result = await _sut.ScanFullInvestments(project.Id.Value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanFullInvestments_WhenIndexerThrows_ReturnsFailure()
    {
        // Arrange
        var project = TestDataBuilder.CreateProject().Build();
        
        _mockProjectService
            .Setup(x => x.GetAsync(It.IsAny<ProjectId>()))
            .ReturnsAsync(Result.Success(project));
        
        _mockAngorIndexerService
            .Setup(x => x.GetInvestmentsAsync(project.Id.Value))
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await _sut.ScanFullInvestments(project.Id.Value);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Network error");
    }

    [Fact]
    public async Task ScanFullInvestments_CallsProjectServiceWithCorrectId()
    {
        // Arrange
        var projectId = "specific-project-id";
        var project = TestDataBuilder.CreateProject().WithId(projectId).Build();
        
        _mockProjectService
            .Setup(x => x.GetAsync(It.Is<ProjectId>(p => p.Value == projectId)))
            .ReturnsAsync(Result.Success(project));
        
        _mockAngorIndexerService
            .Setup(x => x.GetInvestmentsAsync(projectId))
            .ReturnsAsync(new List<ProjectInvestment>());

        // Act
        await _sut.ScanFullInvestments(projectId);

        // Assert
        _mockProjectService.Verify(
            x => x.GetAsync(It.Is<ProjectId>(p => p.Value == projectId)), 
            Times.Once);
    }

    [Fact]
    public async Task ScanFullInvestments_CallsIndexerServiceWithCorrectProjectId()
    {
        // Arrange
        var projectId = "test-project-123";
        var project = TestDataBuilder.CreateProject().WithId(projectId).Build();
        
        _mockProjectService
            .Setup(x => x.GetAsync(It.IsAny<ProjectId>()))
            .ReturnsAsync(Result.Success(project));
        
        _mockAngorIndexerService
            .Setup(x => x.GetInvestmentsAsync(projectId))
            .ReturnsAsync(new List<ProjectInvestment>());

        // Act
        await _sut.ScanFullInvestments(projectId);

        // Assert
        _mockAngorIndexerService.Verify(
            x => x.GetInvestmentsAsync(projectId), 
            Times.Once);
    }

    #endregion

    #region CheckSpentFund Tests

    [Fact]
    public async Task CheckSpentFund_WhenOutputIsNull_ReturnsFailure()
    {
        // Arrange
        var projectInfo = TestDataBuilder.CreateProjectInfo().Build();
        var transaction = _fixture.Network.CreateTransaction();

        // Act
        var result = await _sut.CheckSpentFund(null, transaction, projectInfo, 0);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Output not found");
    }

    [Fact]
    public async Task CheckSpentFund_WhenOutputNotSpent_ReturnsUnspentItem()
    {
        // Arrange
        var projectInfo = TestDataBuilder.CreateProjectInfo().Build();
        
        // Create a valid investment transaction with taproot outputs
        var transaction = CreateMockInvestmentTransaction(projectInfo, 3);
        
        var output = new QueryTransactionOutput
        {
            Index = 2, // First stage output (index 2 after anchor outputs)
            Balance = 100000,
            SpentInTransaction = null // Not spent
        };

        // Act
        var result = await _sut.CheckSpentFund(output, transaction, projectInfo, 0);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsSpent.Should().BeFalse();
        result.Value.Trxid.Should().Be(transaction.GetHash().ToString());
    }

    [Fact(Skip = "Requires properly constructed taproot transactions with valid scripts. The InvestorTransactionActions.DiscoverUsedScript method needs real taproot scripts to function properly.")]
    public async Task CheckSpentFund_WhenOutputSpent_ReturnsSpentItem()
    {
        // Arrange
        var projectInfo = TestDataBuilder.CreateProjectInfo().Build();
        var transaction = CreateMockInvestmentTransaction(projectInfo, 3);
        var spentInTxId = "spent-tx-id-123";
        
        var output = new QueryTransactionOutput
        {
            Index = 2,
            Balance = 100000,
            SpentInTransaction = spentInTxId
        };
        
        // Mock the spent transaction info
        var spentTxInfo = TestDataBuilder.CreateQueryTransaction()
            .WithTransactionId(spentInTxId)
            .AddInput(transaction.GetHash().ToString(), 2, "")
            .Build();
        
        _mockTransactionService
            .Setup(x => x.GetTransactionInfoByIdAsync(spentInTxId))
            .ReturnsAsync(spentTxInfo);

        // Act
        var result = await _sut.CheckSpentFund(output, transaction, projectInfo, 0);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsSpent.Should().BeTrue();
    }

    #endregion

    #region ScanInvestmentSpends Tests

    [Fact]
    public async Task ScanInvestmentSpends_WhenTransactionNotFound_ReturnsFailure()
    {
        // Arrange
        var projectInfo = TestDataBuilder.CreateProjectInfo().Build();
        var transactionId = "non-existent-tx";
        
        _mockTransactionService
            .Setup(x => x.GetTransactionInfoByIdAsync(transactionId))
            .ReturnsAsync((QueryTransaction?)null);

        // Act
        var result = await _sut.ScanInvestmentSpends(projectInfo, transactionId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Transaction not found");
    }

    [Fact]
    public async Task ScanInvestmentSpends_WhenNoSpends_ReturnsEmptyLookup()
    {
        // Arrange
        var projectInfo = TestDataBuilder.CreateProjectInfo().WithStages(3).Build();
        var transactionId = "test-tx-id";
        
        // Create transaction info with unspent outputs
        var txInfo = TestDataBuilder.CreateQueryTransaction()
            .WithTransactionId(transactionId)
            .AddOutput(0, 0) // Anchor output
            .AddOutput(1, 0) // Anchor output
            .AddOutput(2, 100000) // Stage 0 - unspent
            .AddOutput(3, 100000) // Stage 1 - unspent
            .AddOutput(4, 100000) // Stage 2 - unspent
            .Build();
        
        var txHex = CreateMockInvestmentTransactionHex(projectInfo, 3);
        
        _mockTransactionService
            .Setup(x => x.GetTransactionInfoByIdAsync(transactionId))
            .ReturnsAsync(txInfo);
        
        _mockTransactionService
            .Setup(x => x.GetTransactionHexByIdAsync(transactionId))
            .ReturnsAsync(txHex);

        // Act
        var result = await _sut.ScanInvestmentSpends(projectInfo, transactionId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TransactionId.Should().Be(transactionId);
        result.Value.ProjectIdentifier.Should().Be(projectInfo.ProjectIdentifier);
        result.Value.EndOfProjectTransactionId.Should().BeNullOrEmpty();
        result.Value.RecoveryTransactionId.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task ScanInvestmentSpends_CallsTransactionServiceForInfo()
    {
        // Arrange
        var projectInfo = TestDataBuilder.CreateProjectInfo().Build();
        var transactionId = "test-tx-for-verification";
        
        _mockTransactionService
            .Setup(x => x.GetTransactionInfoByIdAsync(transactionId))
            .ReturnsAsync((QueryTransaction?)null);

        // Act
        await _sut.ScanInvestmentSpends(projectInfo, transactionId);

        // Assert
        _mockTransactionService.Verify(
            x => x.GetTransactionInfoByIdAsync(transactionId), 
            Times.Once);
    }

    [Fact(Skip = "Requires properly constructed taproot transactions with valid scripts. The InvestorTransactionActions.DiscoverUsedScript method needs real taproot scripts to function properly.")]
    public async Task ScanInvestmentSpends_WhenFounderSpent_ContinuesToNextStage()
    {
        // Arrange
        var projectInfo = TestDataBuilder.CreateProjectInfo().WithStages(3).Build();
        var transactionId = "investment-tx-id";
        var founderSpentTxId = "founder-spent-tx-id";
        
        // Create transaction info where stage 0 is spent by founder
        var txInfo = TestDataBuilder.CreateQueryTransaction()
            .WithTransactionId(transactionId)
            .AddOutput(0, 0)
            .AddOutput(1, 0)
            .AddOutput(2, 100000, founderSpentTxId) // Stage 0 - spent by founder
            .AddOutput(3, 100000) // Stage 1 - unspent
            .AddOutput(4, 100000) // Stage 2 - unspent
            .Build();
        
        var txHex = CreateMockInvestmentTransactionHex(projectInfo, 3);
        
        _mockTransactionService
            .Setup(x => x.GetTransactionInfoByIdAsync(transactionId))
            .ReturnsAsync(txInfo);
        
        _mockTransactionService
            .Setup(x => x.GetTransactionHexByIdAsync(transactionId))
            .ReturnsAsync(txHex);
        
        // The spent transaction would have inputs indicating founder script was used
        var founderSpentInfo = TestDataBuilder.CreateQueryTransaction()
            .WithTransactionId(founderSpentTxId)
            .AddInput(transactionId, 2, "founder-witness-script")
            .Build();
        
        _mockTransactionService
            .Setup(x => x.GetTransactionInfoByIdAsync(founderSpentTxId))
            .ReturnsAsync(founderSpentInfo);

        // Act
        var result = await _sut.ScanInvestmentSpends(projectInfo, transactionId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Founder spends don't set the recovery/end-of-project fields - processing continues
        result.Value.RecoveryTransactionId.Should().BeNullOrEmpty();
        result.Value.EndOfProjectTransactionId.Should().BeNullOrEmpty();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a mock investment transaction with the specified number of taproot stage outputs.
    /// </summary>
    private Transaction CreateMockInvestmentTransaction(ProjectInfo projectInfo, int stageCount)
    {
        var transaction = _fixture.Network.CreateTransaction();
        
        // Add anchor outputs (first 2 outputs)
        transaction.Outputs.Add(new TxOut(Money.Zero, new Script()));
        transaction.Outputs.Add(new TxOut(Money.Zero, new Script()));
        
        // Add stage outputs (taproot)
        for (int i = 0; i < stageCount; i++)
        {
            // Create a taproot output (version 1, 32-byte pubkey)
            var taprootPubKey = new byte[32];
            Random.Shared.NextBytes(taprootPubKey);
            var taprootScript = new Script(new[] { (byte)0x51, (byte)0x20 }.Concat(taprootPubKey).ToArray());
            transaction.Outputs.Add(new TxOut(Money.Satoshis(100000), taprootScript));
        }
        
        return transaction;
    }

    /// <summary>
    /// Creates a hex representation of a mock investment transaction.
    /// </summary>
    private string CreateMockInvestmentTransactionHex(ProjectInfo projectInfo, int stageCount)
    {
        var transaction = CreateMockInvestmentTransaction(projectInfo, stageCount);
        return transaction.ToHex();
    }

    #endregion
}
