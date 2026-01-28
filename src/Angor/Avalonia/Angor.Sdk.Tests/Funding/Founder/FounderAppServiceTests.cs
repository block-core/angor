using Angor.Data.Documents.Interfaces;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Tests.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Angor.Sdk.Tests.Funding.Founder;

/// <summary>
/// Unit tests for Founder App Service handlers.
/// Tests the GetProjectInvestments, CreateProjectKeys, and GetReleasableTransactions handlers.
/// </summary>
public class FounderAppServiceTests : IClassFixture<TestNetworkFixture>
{
    private readonly TestNetworkFixture _fixture;
    private readonly Mock<IProjectService> _mockProjectService;
    private readonly Mock<IAngorIndexerService> _mockAngorIndexerService;
    private readonly Mock<IInvestmentHandshakeService> _mockInvestmentHandshakeService;
    private readonly Mock<IGenericDocumentCollection<DerivedProjectKeys>> _mockDerivedProjectKeysCollection;

    public FounderAppServiceTests(TestNetworkFixture fixture)
    {
        _fixture = fixture;
        _mockProjectService = new Mock<IProjectService>();
        _mockAngorIndexerService = new Mock<IAngorIndexerService>();
        _mockInvestmentHandshakeService = new Mock<IInvestmentHandshakeService>();
        _mockDerivedProjectKeysCollection = new Mock<IGenericDocumentCollection<DerivedProjectKeys>>();
    }

    #region GetProjectInvestmentsHandler Tests

    [Fact]
    public async Task GetProjectInvestmentsHandler_WhenProjectNotFound_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var projectId = new ProjectId("test-project");
        
        _mockProjectService
            .Setup(x => x.GetAsync(projectId))
            .ReturnsAsync(Result.Failure<Project>("Project not found"));
        
        var handler = new GetProjectInvestments.GetProjectInvestmentsHandler(
            _mockAngorIndexerService.Object,
            _mockProjectService.Object,
            _fixture.NetworkConfiguration,
            _mockInvestmentHandshakeService.Object);
        
        var request = new GetProjectInvestments.GetProjectInvestmentsRequest(walletId, projectId);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Project not found");
    }

    [Fact]
    public async Task GetProjectInvestmentsHandler_WhenHandshakeSyncFails_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var project = TestDataBuilder.CreateProject().Build();
        
        _mockProjectService
            .Setup(x => x.GetAsync(project.Id))
            .ReturnsAsync(Result.Success(project));
        
        _mockInvestmentHandshakeService
            .Setup(x => x.SyncHandshakesFromNostrAsync(walletId, project.Id, project.NostrPubKey))
            .ReturnsAsync(Result.Failure<IEnumerable<InvestmentHandshake>>("Nostr sync failed"));
        
        var handler = new GetProjectInvestments.GetProjectInvestmentsHandler(
            _mockAngorIndexerService.Object,
            _mockProjectService.Object,
            _fixture.NetworkConfiguration,
            _mockInvestmentHandshakeService.Object);
        
        var request = new GetProjectInvestments.GetProjectInvestmentsRequest(walletId, project.Id);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Nostr sync failed");
    }

    [Fact]
    public async Task GetProjectInvestmentsHandler_WhenNoHandshakes_ReturnsEmptyList()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var project = TestDataBuilder.CreateProject().Build();
        
        _mockProjectService
            .Setup(x => x.GetAsync(project.Id))
            .ReturnsAsync(Result.Success(project));
        
        _mockInvestmentHandshakeService
            .Setup(x => x.SyncHandshakesFromNostrAsync(walletId, project.Id, project.NostrPubKey))
            .ReturnsAsync(Result.Success<IEnumerable<InvestmentHandshake>>(Enumerable.Empty<InvestmentHandshake>()));
        
        _mockInvestmentHandshakeService
            .Setup(x => x.GetHandshakesAsync(walletId, project.Id))
            .ReturnsAsync(Result.Success<IEnumerable<InvestmentHandshake>>(Enumerable.Empty<InvestmentHandshake>()));
        
        _mockAngorIndexerService
            .Setup(x => x.GetInvestmentsAsync(project.Id.Value))
            .ReturnsAsync(new List<ProjectInvestment>());
        
        var handler = new GetProjectInvestments.GetProjectInvestmentsHandler(
            _mockAngorIndexerService.Object,
            _mockProjectService.Object,
            _fixture.NetworkConfiguration,
            _mockInvestmentHandshakeService.Object);
        
        var request = new GetProjectInvestments.GetProjectInvestmentsRequest(walletId, project.Id);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Investments.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProjectInvestmentsHandler_CallsProjectServiceWithCorrectId()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var projectId = new ProjectId("specific-project-id");
        
        _mockProjectService
            .Setup(x => x.GetAsync(projectId))
            .ReturnsAsync(Result.Failure<Project>("Not found"));
        
        var handler = new GetProjectInvestments.GetProjectInvestmentsHandler(
            _mockAngorIndexerService.Object,
            _mockProjectService.Object,
            _fixture.NetworkConfiguration,
            _mockInvestmentHandshakeService.Object);
        
        var request = new GetProjectInvestments.GetProjectInvestmentsRequest(walletId, projectId);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        _mockProjectService.Verify(x => x.GetAsync(projectId), Times.Once);
    }

    [Fact]
    public async Task GetProjectInvestmentsHandler_SyncsHandshakesFromNostr()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var project = TestDataBuilder.CreateProject().Build();
        
        _mockProjectService
            .Setup(x => x.GetAsync(project.Id))
            .ReturnsAsync(Result.Success(project));
        
        _mockInvestmentHandshakeService
            .Setup(x => x.SyncHandshakesFromNostrAsync(walletId, project.Id, project.NostrPubKey))
            .ReturnsAsync(Result.Success<IEnumerable<InvestmentHandshake>>(Enumerable.Empty<InvestmentHandshake>()));
        
        _mockInvestmentHandshakeService
            .Setup(x => x.GetHandshakesAsync(walletId, project.Id))
            .ReturnsAsync(Result.Success<IEnumerable<InvestmentHandshake>>(Enumerable.Empty<InvestmentHandshake>()));
        
        _mockAngorIndexerService
            .Setup(x => x.GetInvestmentsAsync(project.Id.Value))
            .ReturnsAsync(new List<ProjectInvestment>());
        
        var handler = new GetProjectInvestments.GetProjectInvestmentsHandler(
            _mockAngorIndexerService.Object,
            _mockProjectService.Object,
            _fixture.NetworkConfiguration,
            _mockInvestmentHandshakeService.Object);
        
        var request = new GetProjectInvestments.GetProjectInvestmentsRequest(walletId, project.Id);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        _mockInvestmentHandshakeService.Verify(
            x => x.SyncHandshakesFromNostrAsync(walletId, project.Id, project.NostrPubKey), 
            Times.Once);
    }

    [Fact]
    public async Task GetProjectInvestmentsHandler_LooksUpCurrentInvestmentsFromIndexer()
    {
        // Arrange
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var project = TestDataBuilder.CreateProject().Build();
        
        _mockProjectService
            .Setup(x => x.GetAsync(project.Id))
            .ReturnsAsync(Result.Success(project));
        
        _mockInvestmentHandshakeService
            .Setup(x => x.SyncHandshakesFromNostrAsync(walletId, project.Id, project.NostrPubKey))
            .ReturnsAsync(Result.Success<IEnumerable<InvestmentHandshake>>(Enumerable.Empty<InvestmentHandshake>()));
        
        _mockInvestmentHandshakeService
            .Setup(x => x.GetHandshakesAsync(walletId, project.Id))
            .ReturnsAsync(Result.Success<IEnumerable<InvestmentHandshake>>(Enumerable.Empty<InvestmentHandshake>()));
        
        _mockAngorIndexerService
            .Setup(x => x.GetInvestmentsAsync(project.Id.Value))
            .ReturnsAsync(new List<ProjectInvestment>());
        
        var handler = new GetProjectInvestments.GetProjectInvestmentsHandler(
            _mockAngorIndexerService.Object,
            _mockProjectService.Object,
            _fixture.NetworkConfiguration,
            _mockInvestmentHandshakeService.Object);
        
        var request = new GetProjectInvestments.GetProjectInvestmentsRequest(walletId, project.Id);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        _mockAngorIndexerService.Verify(
            x => x.GetInvestmentsAsync(project.Id.Value), 
            Times.Once);
    }

    #endregion
}
