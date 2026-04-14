using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder.Domain;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using Stage = Angor.Sdk.Funding.Projects.Domain.Stage;

namespace Angor.Sdk.Tests.Funding.Founder;

public class GetFounderProjectsTests
{
    private readonly Mock<IProjectService> _mockProjectService;
    private readonly Mock<IFounderProjectsService> _mockFounderProjectsService;
    private readonly GetFounderProjects.GetFounderProjectsHandler _sut;

    public GetFounderProjectsTests()
    {
        _mockProjectService = new Mock<IProjectService>();
        _mockFounderProjectsService = new Mock<IFounderProjectsService>();
        _sut = new GetFounderProjects.GetFounderProjectsHandler(
            _mockProjectService.Object,
            _mockFounderProjectsService.Object);
    }

    [Fact]
    public async Task Handle_WhenFounderProjectsServiceFails_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var request = new GetFounderProjects.GetFounderProjectsRequest(walletId);

        _mockFounderProjectsService
            .Setup(x => x.GetByWalletId(walletId.Value))
            .ReturnsAsync(Result.Failure<List<FounderProjectRecord>>("Storage error"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Storage error");
    }

    [Fact]
    public async Task Handle_WhenNoRecords_ReturnsEmptyList()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var request = new GetFounderProjects.GetFounderProjectsRequest(walletId);

        _mockFounderProjectsService
            .Setup(x => x.GetByWalletId(walletId.Value))
            .ReturnsAsync(Result.Success(new List<FounderProjectRecord>()));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Projects.Should().BeEmpty();
        _mockProjectService.Verify(x => x.GetAllAsync(It.IsAny<ProjectId[]>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenProjectServiceFails_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var request = new GetFounderProjects.GetFounderProjectsRequest(walletId);

        _mockFounderProjectsService
            .Setup(x => x.GetByWalletId(walletId.Value))
            .ReturnsAsync(Result.Success(new List<FounderProjectRecord>
            {
                new FounderProjectRecord { ProjectIdentifier = "project-1" }
            }));

        _mockProjectService
            .Setup(x => x.GetAllAsync(It.IsAny<ProjectId[]>()))
            .ReturnsAsync(Result.Failure<IEnumerable<Project>>("Indexer unavailable"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Indexer unavailable");
    }

    [Fact]
    public async Task Handle_WhenProjectsExist_ReturnsSortedByDateDescending()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var request = new GetFounderProjects.GetFounderProjectsRequest(walletId);

        var records = new List<FounderProjectRecord>
        {
            new FounderProjectRecord { ProjectIdentifier = "project-1" },
            new FounderProjectRecord { ProjectIdentifier = "project-2" }
        };

        _mockFounderProjectsService
            .Setup(x => x.GetByWalletId(walletId.Value))
            .ReturnsAsync(Result.Success(records));

        var olderDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newerDate = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        var projects = new[]
        {
            CreateTestProject("project-1", "Older Project", olderDate),
            CreateTestProject("project-2", "Newer Project", newerDate)
        };

        _mockProjectService
            .Setup(x => x.GetAllAsync(It.IsAny<ProjectId[]>()))
            .ReturnsAsync(Result.Success<IEnumerable<Project>>(projects));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var projectList = result.Value.Projects.ToList();
        projectList.Should().HaveCount(2);
        projectList[0].Name.Should().Be("Newer Project", "Should be sorted by date descending");
        projectList[1].Name.Should().Be("Older Project");
    }

    [Fact]
    public async Task Handle_PassesCorrectProjectIdsToGetAll()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var request = new GetFounderProjects.GetFounderProjectsRequest(walletId);

        var records = new List<FounderProjectRecord>
        {
            new FounderProjectRecord { ProjectIdentifier = "proj-abc" },
            new FounderProjectRecord { ProjectIdentifier = "proj-xyz" }
        };

        _mockFounderProjectsService
            .Setup(x => x.GetByWalletId(walletId.Value))
            .ReturnsAsync(Result.Success(records));

        ProjectId[]? capturedIds = null;
        _mockProjectService
            .Setup(x => x.GetAllAsync(It.IsAny<ProjectId[]>()))
            .Callback<ProjectId[]>(ids => capturedIds = ids)
            .ReturnsAsync(Result.Success(Enumerable.Empty<Project>()));

        // Act
        await _sut.Handle(request, CancellationToken.None);

        // Assert
        capturedIds.Should().NotBeNull();
        capturedIds!.Select(id => id.Value).Should().BeEquivalentTo(new[] { "proj-abc", "proj-xyz" });
    }

    private static Project CreateTestProject(string id, string name, DateTime startDate)
    {
        return new Project
        {
            Id = new ProjectId(id),
            Name = name,
            FounderKey = "founder-key",
            FounderRecoveryKey = "recovery-key",
            NostrPubKey = "nostr-pub",
            ShortDescription = $"Test project {name}",
            TargetAmount = 1_000_000,
            StartingDate = startDate,
            ExpiryDate = startDate.AddYears(1),
            EndDate = startDate.AddYears(1),
            Stages = new List<Stage>()
        };
    }
}
