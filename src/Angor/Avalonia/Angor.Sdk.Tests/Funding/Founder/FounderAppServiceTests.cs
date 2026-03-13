using Angor.Data.Documents.Interfaces;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Projects.Dtos;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Tests.Shared;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Blockcore.NBitcoin;
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
    private readonly Mock<ISeedwordsProvider> _mockSeedwordsProvider;
    private readonly Mock<IDerivationOperations> _mockDerivationOperations;
    private readonly Mock<IRelayService> _mockRelayService;

    public FounderAppServiceTests(TestNetworkFixture fixture)
    {
        _fixture = fixture;
        _mockProjectService = new Mock<IProjectService>();
        _mockAngorIndexerService = new Mock<IAngorIndexerService>();
        _mockInvestmentHandshakeService = new Mock<IInvestmentHandshakeService>();
        _mockDerivedProjectKeysCollection = new Mock<IGenericDocumentCollection<DerivedProjectKeys>>();
        _mockSeedwordsProvider = new Mock<ISeedwordsProvider>();
        _mockDerivationOperations = new Mock<IDerivationOperations>();
        _mockRelayService = new Mock<IRelayService>();
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

    [Fact]
    public async Task CreateProjectInfoHandler_WhenInvestStagePercentagesDoNotTotal100_ReturnsFailure()
    {
        var walletId = new WalletId(Guid.NewGuid().ToString());
        var founderKeys = new ProjectSeedDto("founder", "recovery", "nostr-pub", "project-id");
        var project = new CreateProjectDto
        {
            ProjectName = "Test Project",
            Description = "Test description",
            AvatarUri = "https://example.com/avatar.png",
            BannerUri = "https://example.com/banner.png",
            ProjectType = ProjectType.Invest,
            Sats = 100_000,
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays(10),
            ExpiryDate = DateTime.Today.AddDays(40),
            TargetAmount = new Amount(100_000),
            PenaltyDays = 10,
            PenaltyThreshold = 0,
            Stages =
            [
                new CreateProjectStageDto(DateOnly.FromDateTime(DateTime.Today.AddDays(15)), 33),
                new CreateProjectStageDto(DateOnly.FromDateTime(DateTime.Today.AddDays(30)), 33),
                new CreateProjectStageDto(DateOnly.FromDateTime(DateTime.Today.AddDays(45)), 33)
            ],
            SelectedPatterns = null,
            PayoutDay = null
        };

        _mockSeedwordsProvider
            .Setup(x => x.GetSensitiveData(walletId.Value))
            .ReturnsAsync(Result.Success((TestNetworkFixture.AlternateWalletWords, Maybe<string>.None)));

        _mockDerivationOperations
            .Setup(x => x.DeriveProjectNostrPrivateKeyAsync(It.IsAny<WalletWords>(), founderKeys.FounderKey))
            .ReturnsAsync(new Key());

        var handler = new CreateProjectInfo.CreateProjectInfoHandler(
            _mockSeedwordsProvider.Object,
            _mockDerivationOperations.Object,
            _mockRelayService.Object,
            _mockAngorIndexerService.Object,
            _mockDerivedProjectKeysCollection.Object,
            NullLogger<CreateProjectInfo.CreateProjectInfoHandler>.Instance);

        var request = new CreateProjectInfo.CreateProjectInfoRequest(walletId, project, founderKeys);

        var result = await handler.Handle(request, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("100%");
        _mockRelayService.Verify(x => x.AddProjectAsync(It.IsAny<ProjectInfo>(), It.IsAny<string>(), It.IsAny<Action<Nostr.Client.Responses.NostrOkResponse>>()), Times.Never);
    }
}
