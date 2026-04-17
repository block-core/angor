using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder.Domain;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Data.Documents.Interfaces;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;

namespace Angor.Sdk.Tests.Funding.Founder;

public class ScanFounderProjectsTests
{
    private readonly Mock<IProjectService> _mockProjectService;
    private readonly Mock<IFounderProjectsService> _mockFounderProjectsService;
    private readonly Mock<IGenericDocumentCollection<DerivedProjectKeys>> _mockDerivedKeysCollection;
    private readonly ScanFounderProjects.ScanFounderProjectsHandler _sut;

    public ScanFounderProjectsTests()
    {
        _mockProjectService = new Mock<IProjectService>();
        _mockFounderProjectsService = new Mock<IFounderProjectsService>();
        _mockDerivedKeysCollection = new Mock<IGenericDocumentCollection<DerivedProjectKeys>>();

        _sut = new ScanFounderProjects.ScanFounderProjectsHandler(
            _mockProjectService.Object,
            _mockFounderProjectsService.Object,
            _mockDerivedKeysCollection.Object);
    }

    [Fact]
    public async Task Handle_WhenDerivedKeysFails_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var request = new ScanFounderProjects.ScanFounderProjectsRequest(walletId);

        _mockDerivedKeysCollection
            .Setup(x => x.FindByIdAsync("wallet-1"))
            .ReturnsAsync(Result.Failure<DerivedProjectKeys?>("Storage error"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Storage error");
    }

    [Fact]
    public async Task Handle_WhenDerivedKeysReturnsNull_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var request = new ScanFounderProjects.ScanFounderProjectsRequest(walletId);

        _mockDerivedKeysCollection
            .Setup(x => x.FindByIdAsync("wallet-1"))
            .ReturnsAsync(Result.Success<DerivedProjectKeys?>(null));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No derived keys found");
    }

    [Fact]
    public async Task Handle_WhenNoUnknownKeysAndNoLocal_ReturnsEmptyList()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var request = new ScanFounderProjects.ScanFounderProjectsRequest(walletId);

        var derivedKeys = new DerivedProjectKeys
        {
            WalletId = "wallet-1",
            Keys = new List<FounderKeys>()
        };
        _mockDerivedKeysCollection
            .Setup(x => x.FindByIdAsync("wallet-1"))
            .ReturnsAsync(Result.Success<DerivedProjectKeys?>(derivedKeys));

        _mockFounderProjectsService
            .Setup(x => x.GetByWalletId("wallet-1"))
            .ReturnsAsync(Result.Success(new List<FounderProjectRecord>()));

        _mockProjectService
            .Setup(x => x.GetAllAsync(It.IsAny<ProjectId[]>()))
            .ReturnsAsync(Result.Failure<IEnumerable<Project>>("No projects found"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Projects.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenAllKeysAlreadyKnown_SkipsScanAndReturnsProjects()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var request = new ScanFounderProjects.ScanFounderProjectsRequest(walletId);

        var derivedKeys = new DerivedProjectKeys
        {
            WalletId = "wallet-1",
            Keys = new List<FounderKeys>
            {
                new FounderKeys { ProjectIdentifier = "proj-1" }
            }
        };
        _mockDerivedKeysCollection
            .Setup(x => x.FindByIdAsync("wallet-1"))
            .ReturnsAsync(Result.Success<DerivedProjectKeys?>(derivedKeys));

        _mockFounderProjectsService
            .Setup(x => x.GetByWalletId("wallet-1"))
            .ReturnsAsync(Result.Success(new List<FounderProjectRecord>
            {
                new FounderProjectRecord { ProjectIdentifier = "proj-1" }
            }));

        var project = CreateTestProject(new ProjectId("proj-1"));
        _mockProjectService
            .Setup(x => x.GetAllAsync(It.Is<ProjectId[]>(ids => ids.Length == 1 && ids[0].Value == "proj-1")))
            .ReturnsAsync(Result.Success<IEnumerable<Project>>(new[] { project }));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Projects.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_WhenNewProjectsDiscovered_PersistsAndReturnsThem()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var request = new ScanFounderProjects.ScanFounderProjectsRequest(walletId);

        var derivedKeys = new DerivedProjectKeys
        {
            WalletId = "wallet-1",
            Keys = new List<FounderKeys>
            {
                new FounderKeys { ProjectIdentifier = "proj-1" },
                new FounderKeys { ProjectIdentifier = "proj-2" }
            }
        };
        _mockDerivedKeysCollection
            .Setup(x => x.FindByIdAsync("wallet-1"))
            .ReturnsAsync(Result.Success<DerivedProjectKeys?>(derivedKeys));

        // proj-1 is already known locally
        _mockFounderProjectsService
            .Setup(x => x.GetByWalletId("wallet-1"))
            .ReturnsAsync(Result.Success(new List<FounderProjectRecord>
            {
                new FounderProjectRecord { ProjectIdentifier = "proj-1" }
            }));

        // proj-2 is new — indexer scan returns it
        var newProject = CreateTestProject(new ProjectId("proj-2"));
        _mockProjectService
            .Setup(x => x.GetAllAsync(It.Is<ProjectId[]>(ids => ids.Length == 1 && ids[0].Value == "proj-2")))
            .ReturnsAsync(Result.Success<IEnumerable<Project>>(new[] { newProject }));

        _mockFounderProjectsService
            .Setup(x => x.AddRange("wallet-1", It.IsAny<IEnumerable<FounderProjectRecord>>()))
            .ReturnsAsync(Result.Success());

        // After adding, reload returns both
        var callCount = 0;
        _mockFounderProjectsService
            .Setup(x => x.GetByWalletId("wallet-1"))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount <= 1)
                {
                    return Result.Success(new List<FounderProjectRecord>
                    {
                        new FounderProjectRecord { ProjectIdentifier = "proj-1" }
                    });
                }
                return Result.Success(new List<FounderProjectRecord>
                {
                    new FounderProjectRecord { ProjectIdentifier = "proj-1" },
                    new FounderProjectRecord { ProjectIdentifier = "proj-2" }
                });
            });

        var allProjects = new[]
        {
            CreateTestProject(new ProjectId("proj-1")),
            CreateTestProject(new ProjectId("proj-2"))
        };
        _mockProjectService
            .Setup(x => x.GetAllAsync(It.Is<ProjectId[]>(ids => ids.Length == 2)))
            .ReturnsAsync(Result.Success<IEnumerable<Project>>(allProjects));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockFounderProjectsService.Verify(
            x => x.AddRange("wallet-1", It.Is<IEnumerable<FounderProjectRecord>>(
                records => records.Any(r => r.ProjectIdentifier == "proj-2"))),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenScanFailsButLocalProjectsExist_StillReturnsLocalProjects()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var request = new ScanFounderProjects.ScanFounderProjectsRequest(walletId);

        var derivedKeys = new DerivedProjectKeys
        {
            WalletId = "wallet-1",
            Keys = new List<FounderKeys>
            {
                new FounderKeys { ProjectIdentifier = "proj-1" },
                new FounderKeys { ProjectIdentifier = "proj-2" }
            }
        };
        _mockDerivedKeysCollection
            .Setup(x => x.FindByIdAsync("wallet-1"))
            .ReturnsAsync(Result.Success<DerivedProjectKeys?>(derivedKeys));

        _mockFounderProjectsService
            .Setup(x => x.GetByWalletId("wallet-1"))
            .ReturnsAsync(Result.Success(new List<FounderProjectRecord>
            {
                new FounderProjectRecord { ProjectIdentifier = "proj-1" }
            }));

        // Scan for unknown keys fails
        _mockProjectService
            .Setup(x => x.GetAllAsync(It.Is<ProjectId[]>(ids => ids.Length == 1 && ids[0].Value == "proj-2")))
            .ReturnsAsync(Result.Failure<IEnumerable<Project>>("Indexer unavailable"));

        // Return the known project
        var project = CreateTestProject(new ProjectId("proj-1"));
        _mockProjectService
            .Setup(x => x.GetAllAsync(It.Is<ProjectId[]>(ids => ids.Length == 1 && ids[0].Value == "proj-1")))
            .ReturnsAsync(Result.Success<IEnumerable<Project>>(new[] { project }));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Projects.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_WhenFinalProjectLoadFails_ReturnsFailure()
    {
        // Arrange
        var walletId = new WalletId("wallet-1");
        var request = new ScanFounderProjects.ScanFounderProjectsRequest(walletId);

        var derivedKeys = new DerivedProjectKeys
        {
            WalletId = "wallet-1",
            Keys = new List<FounderKeys>
            {
                new FounderKeys { ProjectIdentifier = "proj-1" }
            }
        };
        _mockDerivedKeysCollection
            .Setup(x => x.FindByIdAsync("wallet-1"))
            .ReturnsAsync(Result.Success<DerivedProjectKeys?>(derivedKeys));

        _mockFounderProjectsService
            .Setup(x => x.GetByWalletId("wallet-1"))
            .ReturnsAsync(Result.Success(new List<FounderProjectRecord>
            {
                new FounderProjectRecord { ProjectIdentifier = "proj-1" }
            }));

        // Final load of projects fails
        _mockProjectService
            .Setup(x => x.GetAllAsync(It.IsAny<ProjectId[]>()))
            .ReturnsAsync(Result.Failure<IEnumerable<Project>>("Network error"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Network error");
    }

    private static Project CreateTestProject(ProjectId projectId)
    {
        return new Project
        {
            Id = projectId,
            Name = "Test Project",
            FounderKey = "founder-key",
            FounderRecoveryKey = "recovery-key",
            NostrPubKey = "nostr-pub-key",
            ShortDescription = "Test",
            TargetAmount = 1_000_000,
            StartingDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            EndDate = DateTime.UtcNow.AddYears(1),
            Stages = new List<Angor.Sdk.Funding.Projects.Domain.Stage>()
        };
    }
}
