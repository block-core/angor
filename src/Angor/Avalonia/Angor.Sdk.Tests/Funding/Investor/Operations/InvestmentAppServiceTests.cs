using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor;
using Angor.Sdk.Funding.Investor.Domain;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Tests.Shared;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Angor.Sdk.Tests.Funding.Investor.Operations;

/// <summary>
/// Unit tests for Investment App Service handlers.
/// Tests the GetInvestments, GetRecoveryStatus, and GetPenalties handlers.
/// </summary>
public class InvestmentAppServiceTests : IClassFixture<TestNetworkFixture>
{
    private readonly TestNetworkFixture _fixture;
    private readonly Mock<IProjectService> _mockProjectService;
    private readonly Mock<IPortfolioService> _mockPortfolioService;
    private readonly Mock<IAngorIndexerService> _mockAngorIndexerService;
    private readonly Mock<IInvestmentHandshakeService> _mockInvestmentHandshakeService;
    private readonly Mock<ITransactionService> _mockTransactionService;
    private readonly Mock<IProjectInvestmentsService> _mockProjectInvestmentsService;
    private readonly Mock<IInvestmentAppService> _mockInvestmentAppService;

    public InvestmentAppServiceTests(TestNetworkFixture fixture)
    {
        _fixture = fixture;
        _mockProjectService = new Mock<IProjectService>();
        _mockPortfolioService = new Mock<IPortfolioService>();
        _mockAngorIndexerService = new Mock<IAngorIndexerService>();
        _mockInvestmentHandshakeService = new Mock<IInvestmentHandshakeService>();
        _mockTransactionService = new Mock<ITransactionService>();
        _mockProjectInvestmentsService = new Mock<IProjectInvestmentsService>();
        _mockInvestmentAppService = new Mock<IInvestmentAppService>();
    }

    #region GetInvestmentsHandler Tests

    [Fact]
    public async Task GetInvestmentsHandler_WhenPortfolioServiceFails_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        
        _mockPortfolioService
            .Setup(x => x.GetByWalletId(walletId.Value))
            .ReturnsAsync(Result.Failure<InvestmentRecords>("Failed to retrieve investments"));
        
        var handler = new GetInvestments.GetInvestmentsHandler(
            _mockAngorIndexerService.Object,
            _mockPortfolioService.Object,
            _mockProjectService.Object,
            _mockInvestmentHandshakeService.Object,
            _fixture.NetworkConfiguration,
            new NullLogger<GetInvestments.GetInvestmentsHandler>());
        
        var request = new GetInvestments.GetInvestmentsRequest(walletId);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to retrieve investment records");
    }

    [Fact]
    public async Task GetInvestmentsHandler_WhenNoInvestments_ReturnsEmptyList()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var emptyRecords = new InvestmentRecords { ProjectIdentifiers = new List<InvestmentRecord>() };
        
        _mockPortfolioService
            .Setup(x => x.GetByWalletId(walletId.Value))
            .ReturnsAsync(Result.Success(emptyRecords));
        
        var handler = new GetInvestments.GetInvestmentsHandler(
            _mockAngorIndexerService.Object,
            _mockPortfolioService.Object,
            _mockProjectService.Object,
            _mockInvestmentHandshakeService.Object,
            _fixture.NetworkConfiguration,
            new NullLogger<GetInvestments.GetInvestmentsHandler>());
        
        var request = new GetInvestments.GetInvestmentsRequest(walletId);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Projects.Should().BeEmpty();
    }

    [Fact]
    public async Task GetInvestmentsHandler_WhenProjectServiceFails_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var projectId = "test-project";
        var records = new InvestmentRecords
        {
            ProjectIdentifiers = new List<InvestmentRecord>
            {
                new InvestmentRecord { ProjectIdentifier = projectId, InvestorPubKey = "investor-pub-key" }
            }
        };
        
        _mockPortfolioService
            .Setup(x => x.GetByWalletId(walletId.Value))
            .ReturnsAsync(Result.Success(records));
        
        _mockProjectService
            .Setup(x => x.GetAllAsync(It.IsAny<ProjectId[]>()))
            .ReturnsAsync(Result.Failure<IEnumerable<Project>>("Failed to get projects"));
        
        var handler = new GetInvestments.GetInvestmentsHandler(
            _mockAngorIndexerService.Object,
            _mockPortfolioService.Object,
            _mockProjectService.Object,
            _mockInvestmentHandshakeService.Object,
            _fixture.NetworkConfiguration,
            new NullLogger<GetInvestments.GetInvestmentsHandler>());
        
        var request = new GetInvestments.GetInvestmentsRequest(walletId);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to retrieve projects");
    }

    [Fact]
    public async Task GetInvestmentsHandler_CallsPortfolioServiceWithCorrectWalletId()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var emptyRecords = new InvestmentRecords { ProjectIdentifiers = new List<InvestmentRecord>() };
        
        _mockPortfolioService
            .Setup(x => x.GetByWalletId(walletId.Value))
            .ReturnsAsync(Result.Success(emptyRecords));
        
        var handler = new GetInvestments.GetInvestmentsHandler(
            _mockAngorIndexerService.Object,
            _mockPortfolioService.Object,
            _mockProjectService.Object,
            _mockInvestmentHandshakeService.Object,
            _fixture.NetworkConfiguration,
            new NullLogger<GetInvestments.GetInvestmentsHandler>());
        
        var request = new GetInvestments.GetInvestmentsRequest(walletId);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        _mockPortfolioService.Verify(x => x.GetByWalletId(walletId.Value), Times.Once);
    }

    [Fact]
    public async Task GetInvestmentsHandler_WhenInvestmentsExist_QueriesProjectsForAllIds()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var projectId1 = "project-1";
        var projectId2 = "project-2";
        
        var records = new InvestmentRecords
        {
            ProjectIdentifiers = new List<InvestmentRecord>
            {
                new InvestmentRecord { ProjectIdentifier = projectId1, InvestorPubKey = "investor-1" },
                new InvestmentRecord { ProjectIdentifier = projectId2, InvestorPubKey = "investor-2" }
            }
        };
        
        _mockPortfolioService
            .Setup(x => x.GetByWalletId(walletId.Value))
            .ReturnsAsync(Result.Success(records));
        
        _mockProjectService
            .Setup(x => x.GetAllAsync(It.IsAny<ProjectId[]>()))
            .ReturnsAsync(Result.Failure<IEnumerable<Project>>("Not found"));
        
        var handler = new GetInvestments.GetInvestmentsHandler(
            _mockAngorIndexerService.Object,
            _mockPortfolioService.Object,
            _mockProjectService.Object,
            _mockInvestmentHandshakeService.Object,
            _fixture.NetworkConfiguration,
            new NullLogger<GetInvestments.GetInvestmentsHandler>());
        
        var request = new GetInvestments.GetInvestmentsRequest(walletId);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        _mockProjectService.Verify(
            x => x.GetAllAsync(It.Is<ProjectId[]>(ids => 
                ids.Length == 2 && 
                ids.Any(id => id.Value == projectId1) && 
                ids.Any(id => id.Value == projectId2))), 
            Times.Once);
    }

    #endregion

    #region GetRecoveryStatusHandler Tests

    [Fact]
    public async Task GetRecoveryStatusHandler_WhenProjectNotFound_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var projectId = new ProjectId("non-existent-project");
        
        _mockProjectService
            .Setup(x => x.GetAsync(projectId))
            .ReturnsAsync(Result.Failure<Project>("Project not found"));
        
        var handler = new GetRecoveryStatus.GetRecoveryStatusHandler(
            _mockProjectService.Object,
            _mockPortfolioService.Object,
            _mockAngorIndexerService.Object,
            _fixture.NetworkConfiguration,
            _fixture.InvestorTransactionActions,
            _mockProjectInvestmentsService.Object,
            _mockTransactionService.Object,
            _mockInvestmentAppService.Object);
        
        var request = new GetRecoveryStatus.GetRecoveryStatusRequest(walletId, projectId);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Project not found");
    }

    [Fact]
    public async Task GetRecoveryStatusHandler_WhenPortfolioFails_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var project = TestDataBuilder.CreateProject().Build();
        
        _mockProjectService
            .Setup(x => x.GetAsync(project.Id))
            .ReturnsAsync(Result.Success(project));
        
        _mockPortfolioService
            .Setup(x => x.GetByWalletId(walletId.Value))
            .ReturnsAsync(Result.Failure<InvestmentRecords>("Portfolio service error"));
        
        var handler = new GetRecoveryStatus.GetRecoveryStatusHandler(
            _mockProjectService.Object,
            _mockPortfolioService.Object,
            _mockAngorIndexerService.Object,
            _fixture.NetworkConfiguration,
            _fixture.InvestorTransactionActions,
            _mockProjectInvestmentsService.Object,
            _mockTransactionService.Object,
            _mockInvestmentAppService.Object);
        
        var request = new GetRecoveryStatus.GetRecoveryStatusRequest(walletId, project.Id);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Portfolio service error");
    }

    [Fact]
    public async Task GetRecoveryStatusHandler_WhenNoInvestmentsForWallet_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var project = TestDataBuilder.CreateProject().Build();
        var emptyRecords = new InvestmentRecords { ProjectIdentifiers = new List<InvestmentRecord>() };
        
        _mockProjectService
            .Setup(x => x.GetAsync(project.Id))
            .ReturnsAsync(Result.Success(project));
        
        _mockPortfolioService
            .Setup(x => x.GetByWalletId(walletId.Value))
            .ReturnsAsync(Result.Success(emptyRecords));
        
        var handler = new GetRecoveryStatus.GetRecoveryStatusHandler(
            _mockProjectService.Object,
            _mockPortfolioService.Object,
            _mockAngorIndexerService.Object,
            _fixture.NetworkConfiguration,
            _fixture.InvestorTransactionActions,
            _mockProjectInvestmentsService.Object,
            _mockTransactionService.Object,
            _mockInvestmentAppService.Object);
        
        var request = new GetRecoveryStatus.GetRecoveryStatusRequest(walletId, project.Id);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No investments found");
    }

    [Fact]
    public async Task GetRecoveryStatusHandler_WhenNoInvestmentForProject_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var project = TestDataBuilder.CreateProject().Build();
        var records = new InvestmentRecords
        {
            ProjectIdentifiers = new List<InvestmentRecord>
            {
                new InvestmentRecord { ProjectIdentifier = "different-project", InvestorPubKey = "investor-pub-key" }
            }
        };
        
        _mockProjectService
            .Setup(x => x.GetAsync(project.Id))
            .ReturnsAsync(Result.Success(project));
        
        _mockPortfolioService
            .Setup(x => x.GetByWalletId(walletId.Value))
            .ReturnsAsync(Result.Success(records));
        
        var handler = new GetRecoveryStatus.GetRecoveryStatusHandler(
            _mockProjectService.Object,
            _mockPortfolioService.Object,
            _mockAngorIndexerService.Object,
            _fixture.NetworkConfiguration,
            _fixture.InvestorTransactionActions,
            _mockProjectInvestmentsService.Object,
            _mockTransactionService.Object,
            _mockInvestmentAppService.Object);
        
        var request = new GetRecoveryStatus.GetRecoveryStatusRequest(walletId, project.Id);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No investments found for this project");
    }

    [Fact]
    public async Task GetRecoveryStatusHandler_CallsProjectServiceWithCorrectId()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var projectId = new ProjectId("specific-project");
        
        _mockProjectService
            .Setup(x => x.GetAsync(projectId))
            .ReturnsAsync(Result.Failure<Project>("Not found"));
        
        var handler = new GetRecoveryStatus.GetRecoveryStatusHandler(
            _mockProjectService.Object,
            _mockPortfolioService.Object,
            _mockAngorIndexerService.Object,
            _fixture.NetworkConfiguration,
            _fixture.InvestorTransactionActions,
            _mockProjectInvestmentsService.Object,
            _mockTransactionService.Object,
            _mockInvestmentAppService.Object);
        
        var request = new GetRecoveryStatus.GetRecoveryStatusRequest(walletId, projectId);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        _mockProjectService.Verify(x => x.GetAsync(projectId), Times.Once);
    }

    #endregion

    #region GetPenaltiesHandler Tests

    [Fact]
    public async Task GetPenaltiesHandler_WhenPortfolioServiceFails_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        
        _mockPortfolioService
            .Setup(x => x.GetByWalletId(walletId.Value))
            .ReturnsAsync(Result.Failure<InvestmentRecords>("Portfolio lookup failed"));
        
        var handler = new GetPenalties.GetPenaltiesHandler(
            _mockPortfolioService.Object,
            _mockAngorIndexerService.Object,
            _mockTransactionService.Object,
            _mockProjectInvestmentsService.Object,
            _mockProjectService.Object);
        
        var request = new GetPenalties.GetPenaltiesRequest(walletId);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Portfolio lookup failed");
    }

    [Fact]
    public async Task GetPenaltiesHandler_WhenNoInvestments_ReturnsEmptyList()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var emptyRecords = new InvestmentRecords { ProjectIdentifiers = new List<InvestmentRecord>() };
        
        _mockPortfolioService
            .Setup(x => x.GetByWalletId(walletId.Value))
            .ReturnsAsync(Result.Success(emptyRecords));
        
        var handler = new GetPenalties.GetPenaltiesHandler(
            _mockPortfolioService.Object,
            _mockAngorIndexerService.Object,
            _mockTransactionService.Object,
            _mockProjectInvestmentsService.Object,
            _mockProjectService.Object);
        
        var request = new GetPenalties.GetPenaltiesRequest(walletId);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Penalties.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPenaltiesHandler_CallsPortfolioServiceWithCorrectWalletId()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var emptyRecords = new InvestmentRecords { ProjectIdentifiers = new List<InvestmentRecord>() };
        
        _mockPortfolioService
            .Setup(x => x.GetByWalletId(walletId.Value))
            .ReturnsAsync(Result.Success(emptyRecords));
        
        var handler = new GetPenalties.GetPenaltiesHandler(
            _mockPortfolioService.Object,
            _mockAngorIndexerService.Object,
            _mockTransactionService.Object,
            _mockProjectInvestmentsService.Object,
            _mockProjectService.Object);
        
        var request = new GetPenalties.GetPenaltiesRequest(walletId);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        _mockPortfolioService.Verify(x => x.GetByWalletId(walletId.Value), Times.Once);
    }

    [Fact]
    public async Task GetPenaltiesHandler_WhenInvestmentsExist_QueriesIndexerForEach()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var projectId = "test-project";
        var investorPubKey = "investor-pub-key";
        
        var records = new InvestmentRecords
        {
            ProjectIdentifiers = new List<InvestmentRecord>
            {
                new InvestmentRecord { ProjectIdentifier = projectId, InvestorPubKey = investorPubKey }
            }
        };
        
        var projects = new List<Project> { TestDataBuilder.CreateProject().WithId(projectId).Build() };
        
        _mockPortfolioService
            .Setup(x => x.GetByWalletId(walletId.Value))
            .ReturnsAsync(Result.Success(records));
        
        _mockAngorIndexerService
            .Setup(x => x.GetInvestmentAsync(projectId, investorPubKey))
            .ReturnsAsync((ProjectInvestment?)null);
        
        _mockProjectService
            .Setup(x => x.GetAllAsync(It.IsAny<ProjectId[]>()))
            .ReturnsAsync(Result.Success<IEnumerable<Project>>(projects));
        
        var handler = new GetPenalties.GetPenaltiesHandler(
            _mockPortfolioService.Object,
            _mockAngorIndexerService.Object,
            _mockTransactionService.Object,
            _mockProjectInvestmentsService.Object,
            _mockProjectService.Object);
        
        var request = new GetPenalties.GetPenaltiesRequest(walletId);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        _mockAngorIndexerService.Verify(
            x => x.GetInvestmentAsync(projectId, investorPubKey), 
            Times.Once);
    }

    #endregion
}
