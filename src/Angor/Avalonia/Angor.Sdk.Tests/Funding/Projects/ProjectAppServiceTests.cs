using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Projects.Operations;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Tests.Shared;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using Xunit;

namespace Angor.Sdk.Tests.Funding.Projects;

/// <summary>
/// Unit tests for Project App Service handlers.
/// Tests the LatestProjects, TryGetProject, GetProject, and ProjectStatistics handlers.
/// </summary>
public class ProjectAppServiceTests : IClassFixture<TestNetworkFixture>
{
    private readonly TestNetworkFixture _fixture;
    private readonly Mock<IProjectService> _mockProjectService;
    private readonly Mock<IProjectInvestmentsService> _mockProjectInvestmentsService;

    public ProjectAppServiceTests(TestNetworkFixture fixture)
    {
        _fixture = fixture;
        _mockProjectService = new Mock<IProjectService>();
        _mockProjectInvestmentsService = new Mock<IProjectInvestmentsService>();
    }

    #region LatestProjectsHandler Tests

    [Fact]
    public async Task LatestProjectsHandler_WhenProjectsExist_ReturnsProjects()
    {
        // Arrange
        var projects = new List<Project>
        {
            TestDataBuilder.CreateProject().WithName("Project 1").Build(),
            TestDataBuilder.CreateProject().WithName("Project 2").Build(),
            TestDataBuilder.CreateProject().WithName("Project 3").Build()
        };
        
        _mockProjectService
            .Setup(x => x.LatestFromNostrAsync())
            .ReturnsAsync(Result.Success<IEnumerable<Project>>(projects));
        
        var handler = new LatestProjects.LatestProjectsHandler(_mockProjectService.Object);
        var request = new LatestProjects.LatestProjectsRequest();

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Projects.Should().HaveCount(3);
        result.Value.Projects.Select(p => p.Name).Should().Contain("Project 1");
        result.Value.Projects.Select(p => p.Name).Should().Contain("Project 2");
        result.Value.Projects.Select(p => p.Name).Should().Contain("Project 3");
    }

    [Fact]
    public async Task LatestProjectsHandler_WhenNoProjects_ReturnsEmptyList()
    {
        // Arrange
        _mockProjectService
            .Setup(x => x.LatestFromNostrAsync())
            .ReturnsAsync(Result.Success<IEnumerable<Project>>(Enumerable.Empty<Project>()));
        
        var handler = new LatestProjects.LatestProjectsHandler(_mockProjectService.Object);
        var request = new LatestProjects.LatestProjectsRequest();

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Projects.Should().BeEmpty();
    }

    [Fact]
    public async Task LatestProjectsHandler_WhenServiceFails_ReturnsFailure()
    {
        // Arrange
        _mockProjectService
            .Setup(x => x.LatestFromNostrAsync())
            .ReturnsAsync(Result.Failure<IEnumerable<Project>>("Failed to fetch projects from Nostr"));
        
        var handler = new LatestProjects.LatestProjectsHandler(_mockProjectService.Object);
        var request = new LatestProjects.LatestProjectsRequest();

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to fetch projects");
    }

    [Fact]
    public async Task LatestProjectsHandler_CallsLatestFromNostrAsync()
    {
        // Arrange
        _mockProjectService
            .Setup(x => x.LatestFromNostrAsync())
            .ReturnsAsync(Result.Success<IEnumerable<Project>>(Enumerable.Empty<Project>()));
        
        var handler = new LatestProjects.LatestProjectsHandler(_mockProjectService.Object);
        var request = new LatestProjects.LatestProjectsRequest();

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        _mockProjectService.Verify(x => x.LatestFromNostrAsync(), Times.Once);
    }

    #endregion

    #region GetProjectHandler Tests

    [Fact]
    public async Task GetProjectHandler_WhenProjectExists_ReturnsProject()
    {
        // Arrange
        var projectId = "test-project-id";
        var project = TestDataBuilder.CreateProject()
            .WithId(projectId)
            .WithName("Test Project")
            .Build();
        
        _mockProjectService
            .Setup(x => x.GetAsync(It.Is<ProjectId>(p => p.Value == projectId)))
            .ReturnsAsync(Result.Success(project));
        
        var handler = new GetProject.GetProjectHandler(_mockProjectService.Object);
        var request = new GetProject.GetProjectRequest(new ProjectId(projectId));

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Project.Name.Should().Be("Test Project");
    }

    [Fact]
    public async Task GetProjectHandler_WhenProjectNotFound_ReturnsFailure()
    {
        // Arrange
        var projectId = "non-existent-project";
        
        _mockProjectService
            .Setup(x => x.GetAsync(It.Is<ProjectId>(p => p.Value == projectId)))
            .ReturnsAsync(Result.Failure<Project>("Project not found"));
        
        var handler = new GetProject.GetProjectHandler(_mockProjectService.Object);
        var request = new GetProject.GetProjectRequest(new ProjectId(projectId));

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Project not found");
    }

    [Fact]
    public async Task GetProjectHandler_CallsProjectServiceWithCorrectId()
    {
        // Arrange
        var projectId = "specific-project-id";
        var project = TestDataBuilder.CreateProject().WithId(projectId).Build();
        
        _mockProjectService
            .Setup(x => x.GetAsync(It.IsAny<ProjectId>()))
            .ReturnsAsync(Result.Success(project));
        
        var handler = new GetProject.GetProjectHandler(_mockProjectService.Object);
        var request = new GetProject.GetProjectRequest(new ProjectId(projectId));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        _mockProjectService.Verify(
            x => x.GetAsync(It.Is<ProjectId>(p => p.Value == projectId)), 
            Times.Once);
    }

    #endregion

    #region TryGetProjectHandler Tests

    [Fact]
    public async Task TryGetProjectHandler_WhenProjectExists_ReturnsMaybeWithProject()
    {
        // Arrange
        var projectId = "existing-project";
        var project = TestDataBuilder.CreateProject()
            .WithId(projectId)
            .WithName("Existing Project")
            .Build();
        
        _mockProjectService
            .Setup(x => x.TryGetAsync(It.Is<ProjectId>(p => p.Value == projectId)))
            .ReturnsAsync(Result.Success(Maybe<Project>.From(project)));
        
        var handler = new TryGetProject.TryGetProjectHandler(_mockProjectService.Object);
        var request = new TryGetProject.TryGetProjectRequest(new ProjectId(projectId));

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Project.HasValue.Should().BeTrue();
        result.Value.Project.Value.Name.Should().Be("Existing Project");
    }

    [Fact]
    public async Task TryGetProjectHandler_WhenProjectNotFound_ReturnsMaybeNone()
    {
        // Arrange
        var projectId = "non-existent-project";
        
        _mockProjectService
            .Setup(x => x.TryGetAsync(It.Is<ProjectId>(p => p.Value == projectId)))
            .ReturnsAsync(Result.Success(Maybe<Project>.None));
        
        var handler = new TryGetProject.TryGetProjectHandler(_mockProjectService.Object);
        var request = new TryGetProject.TryGetProjectRequest(new ProjectId(projectId));

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Project.HasNoValue.Should().BeTrue();
    }

    [Fact]
    public async Task TryGetProjectHandler_WhenServiceFails_ReturnsFailure()
    {
        // Arrange
        var projectId = "test-project";
        
        _mockProjectService
            .Setup(x => x.TryGetAsync(It.IsAny<ProjectId>()))
            .ReturnsAsync(Result.Failure<Maybe<Project>>("Database connection failed"));
        
        var handler = new TryGetProject.TryGetProjectHandler(_mockProjectService.Object);
        var request = new TryGetProject.TryGetProjectRequest(new ProjectId(projectId));

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Database connection failed");
    }

    [Fact]
    public async Task TryGetProjectHandler_CallsTryGetAsyncWithCorrectId()
    {
        // Arrange
        var projectId = "project-to-try";
        
        _mockProjectService
            .Setup(x => x.TryGetAsync(It.IsAny<ProjectId>()))
            .ReturnsAsync(Result.Success(Maybe<Project>.None));
        
        var handler = new TryGetProject.TryGetProjectHandler(_mockProjectService.Object);
        var request = new TryGetProject.TryGetProjectRequest(new ProjectId(projectId));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        _mockProjectService.Verify(
            x => x.TryGetAsync(It.Is<ProjectId>(p => p.Value == projectId)), 
            Times.Once);
    }

    #endregion

    #region ProjectStatsHandler Tests

    [Fact]
    public async Task ProjectStatsHandler_WhenNoInvestments_ReturnsZeroStats()
    {
        // Arrange
        var projectId = "project-with-no-investments";
        var project = TestDataBuilder.CreateProject().WithId(projectId).Build();
        
        _mockProjectInvestmentsService
            .Setup(x => x.ScanFullInvestments(projectId))
            .ReturnsAsync(Result.Success<IEnumerable<StageData>>(Enumerable.Empty<StageData>()));
        
        _mockProjectService
            .Setup(x => x.GetAsync(It.Is<ProjectId>(p => p.Value == projectId)))
            .ReturnsAsync(Result.Success(project));
        
        var handler = new ProjectStatistics.ProjectStatsHandler(
            _mockProjectInvestmentsService.Object, 
            _mockProjectService.Object);
        var request = new ProjectStatistics.ProjectStatsRequest(new ProjectId(projectId));

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalStages.Should().Be(0);
        result.Value.NextStage.Should().BeNull();
    }

    [Fact]
    public async Task ProjectStatsHandler_WhenScanFails_ReturnsFailure()
    {
        // Arrange
        var projectId = "failing-project";
        
        _mockProjectInvestmentsService
            .Setup(x => x.ScanFullInvestments(projectId))
            .ReturnsAsync(Result.Failure<IEnumerable<StageData>>("Failed to scan investments"));
        
        var handler = new ProjectStatistics.ProjectStatsHandler(
            _mockProjectInvestmentsService.Object, 
            _mockProjectService.Object);
        var request = new ProjectStatistics.ProjectStatsRequest(new ProjectId(projectId));

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to scan investments");
    }

    [Fact]
    public async Task ProjectStatsHandler_WhenStagesExist_ReturnsCorrectStageCount()
    {
        // Arrange
        var projectId = "project-with-investments";
        var project = TestDataBuilder.CreateProject().WithId(projectId).WithStages(3).Build();
        
        var stageData = new List<StageData>
        {
            TestDataBuilder.CreateStageData().WithStageIndex(0).WithStageDate(DateTime.UtcNow.AddDays(-5)).Build(),
            TestDataBuilder.CreateStageData().WithStageIndex(1).WithStageDate(DateTime.UtcNow.AddDays(5)).Build(),
            TestDataBuilder.CreateStageData().WithStageIndex(2).WithStageDate(DateTime.UtcNow.AddDays(15)).Build()
        };
        
        _mockProjectInvestmentsService
            .Setup(x => x.ScanFullInvestments(projectId))
            .ReturnsAsync(Result.Success<IEnumerable<StageData>>(stageData));
        
        _mockProjectService
            .Setup(x => x.GetAsync(It.Is<ProjectId>(p => p.Value == projectId)))
            .ReturnsAsync(Result.Success(project));
        
        var handler = new ProjectStatistics.ProjectStatsHandler(
            _mockProjectInvestmentsService.Object, 
            _mockProjectService.Object);
        var request = new ProjectStatistics.ProjectStatsRequest(new ProjectId(projectId));

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalStages.Should().Be(3);
    }

    [Fact]
    public async Task ProjectStatsHandler_CallsScanFullInvestments()
    {
        // Arrange
        var projectId = "project-to-scan";
        var project = TestDataBuilder.CreateProject().WithId(projectId).Build();
        
        _mockProjectInvestmentsService
            .Setup(x => x.ScanFullInvestments(projectId))
            .ReturnsAsync(Result.Success<IEnumerable<StageData>>(Enumerable.Empty<StageData>()));
        
        _mockProjectService
            .Setup(x => x.GetAsync(It.IsAny<ProjectId>()))
            .ReturnsAsync(Result.Success(project));
        
        var handler = new ProjectStatistics.ProjectStatsHandler(
            _mockProjectInvestmentsService.Object, 
            _mockProjectService.Object);
        var request = new ProjectStatistics.ProjectStatsRequest(new ProjectId(projectId));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        _mockProjectInvestmentsService.Verify(
            x => x.ScanFullInvestments(projectId), 
            Times.Once);
    }

    [Fact]
    public async Task ProjectStatsHandler_CallsProjectServiceForProjectInfo()
    {
        // Arrange
        var projectId = "project-needing-info";
        var project = TestDataBuilder.CreateProject().WithId(projectId).Build();
        
        _mockProjectInvestmentsService
            .Setup(x => x.ScanFullInvestments(projectId))
            .ReturnsAsync(Result.Success<IEnumerable<StageData>>(Enumerable.Empty<StageData>()));
        
        _mockProjectService
            .Setup(x => x.GetAsync(It.Is<ProjectId>(p => p.Value == projectId)))
            .ReturnsAsync(Result.Success(project));
        
        var handler = new ProjectStatistics.ProjectStatsHandler(
            _mockProjectInvestmentsService.Object, 
            _mockProjectService.Object);
        var request = new ProjectStatistics.ProjectStatsRequest(new ProjectId(projectId));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        _mockProjectService.Verify(
            x => x.GetAsync(It.Is<ProjectId>(p => p.Value == projectId)), 
            Times.Once);
    }

    #endregion
}
