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
        
        var handler = new LatestProjects.LatestProjectsHandler(_mockProjectService.Object, _fixture.NetworkConfiguration);
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
        
        var handler = new LatestProjects.LatestProjectsHandler(_mockProjectService.Object, _fixture.NetworkConfiguration);
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
        
        var handler = new LatestProjects.LatestProjectsHandler(_mockProjectService.Object, _fixture.NetworkConfiguration);
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
        
        var handler = new LatestProjects.LatestProjectsHandler(_mockProjectService.Object, _fixture.NetworkConfiguration);
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
    public async Task ProjectStatsHandler_WhenAStageHasUnlocked_NextStageReleaseDateIsTheUpcomingStage()
    {
        // Regression test for the "Next Stage countdown stuck at 0" bug: ReleaseDate was
        // populated from currentStage (already unlocked, in the past) instead of nextStage,
        // so the countdown consumer's `ReleaseDate > UtcNow` check could never pass.

        // Arrange
        var projectId = "project-with-unlocked-stage";
        var project = TestDataBuilder.CreateProject().WithId(projectId).WithStages(3).Build();

        var pastStageDate = DateTime.UtcNow.AddDays(-5);
        var upcomingStageDate = DateTime.UtcNow.AddDays(5);
        var lastStageDate = DateTime.UtcNow.AddDays(15);

        var stageData = new List<StageData>
        {
            TestDataBuilder.CreateStageData().WithStageIndex(0).WithStageDate(pastStageDate).Build(),
            TestDataBuilder.CreateStageData().WithStageIndex(1).WithStageDate(upcomingStageDate).Build(),
            TestDataBuilder.CreateStageData().WithStageIndex(2).WithStageDate(lastStageDate).Build()
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
        result.Value.NextStage.Should().NotBeNull();
        result.Value.NextStage!.ReleaseDate.Should().Be(
            upcomingStageDate, "the countdown must target the next upcoming stage, not the already-unlocked one");
        result.Value.NextStage.ReleaseDate.Should().BeAfter(
            DateTime.UtcNow, "the countdown UI only renders when ReleaseDate is in the future");
        result.Value.NextStage.StageIndex.Should().Be(1);
        result.Value.NextStage.DaysUntilRelease.Should().Be(4, "4 full days remain until UtcNow+5d");
    }

    [Fact]
    public async Task ProjectStatsHandler_WhenAllStagesAreInThePast_NextStageReleaseDateFallsBackToLastStage()
    {
        // Arrange
        var projectId = "project-fully-unlocked";
        var project = TestDataBuilder.CreateProject().WithId(projectId).WithStages(2).Build();

        var firstStageDate = DateTime.UtcNow.AddDays(-10);
        var lastStageDate = DateTime.UtcNow.AddDays(-5);

        var stageData = new List<StageData>
        {
            TestDataBuilder.CreateStageData().WithStageIndex(0).WithStageDate(firstStageDate).Build(),
            TestDataBuilder.CreateStageData().WithStageIndex(1).WithStageDate(lastStageDate).Build()
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
        result.Value.NextStage.Should().NotBeNull();
        result.Value.NextStage!.ReleaseDate.Should().Be(
            lastStageDate, "with no upcoming stage the date falls back to the current (last unlocked) stage");
        result.Value.NextStage.ReleaseDate.Should().BeBefore(
            DateTime.UtcNow, "a fully-unlocked project must not render a countdown");
        result.Value.NextStage.DaysUntilRelease.Should().Be(0);
        result.Value.NextStage.StageIndex.Should().Be(1, "falls back to the last stage index");
    }

    [Fact]
    public async Task ProjectStatsHandler_WhenAllStagesAreInTheFuture_NextStageReleaseDateIsTheEarliestStage()
    {
        // Arrange
        var projectId = "project-not-started";
        var project = TestDataBuilder.CreateProject().WithId(projectId).WithStages(2).Build();

        var firstStageDate = DateTime.UtcNow.AddDays(3);
        var lastStageDate = DateTime.UtcNow.AddDays(30);

        var stageData = new List<StageData>
        {
            TestDataBuilder.CreateStageData().WithStageIndex(0).WithStageDate(firstStageDate).Build(),
            TestDataBuilder.CreateStageData().WithStageIndex(1).WithStageDate(lastStageDate).Build()
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
        result.Value.NextStage.Should().NotBeNull();
        result.Value.NextStage!.ReleaseDate.Should().Be(firstStageDate);
        result.Value.NextStage.StageIndex.Should().Be(0);
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
