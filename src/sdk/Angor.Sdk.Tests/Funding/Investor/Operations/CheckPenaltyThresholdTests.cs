using Angor.Sdk.Common;
using Angor.Sdk.Funding.Investor.Operations;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Sdk.Tests.Shared;
using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Protocol;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using Stage = Angor.Sdk.Funding.Projects.Domain.Stage;

namespace Angor.Sdk.Tests.Funding.Investor.Operations;

public class CheckPenaltyThresholdTests : IClassFixture<TestNetworkFixture>
{
    private readonly TestNetworkFixture _fixture;
    private readonly Mock<IProjectService> _mockProjectService;
    private readonly Mock<IInvestorTransactionActions> _mockInvestorTransactionActions;
    private readonly CheckPenaltyThreshold.CheckPenaltyThresholdHandler _sut;

    public CheckPenaltyThresholdTests(TestNetworkFixture fixture)
    {
        _fixture = fixture;
        _mockProjectService = new Mock<IProjectService>();
        _mockInvestorTransactionActions = new Mock<IInvestorTransactionActions>();
        _sut = new CheckPenaltyThreshold.CheckPenaltyThresholdHandler(
            _mockProjectService.Object,
            _mockInvestorTransactionActions.Object);
    }

    [Fact]
    public async Task Handle_WhenProjectNotFound_ReturnsFailure()
    {
        // Arrange
        var projectId = new ProjectId("nonexistent-project");
        var request = new CheckPenaltyThreshold.CheckPenaltyThresholdRequest(projectId, new Amount(100_000));

        _mockProjectService
            .Setup(x => x.GetAsync(projectId))
            .ReturnsAsync(Result.Failure<Project>("Project not found"));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Project not found");
    }

    [Fact]
    public async Task Handle_WhenProjectIsNotFundType_ReturnsFailure()
    {
        // Arrange
        var projectId = new ProjectId("invest-project");
        var request = new CheckPenaltyThreshold.CheckPenaltyThresholdRequest(projectId, new Amount(100_000));

        var investProject = new Project
        {
            Id = projectId,
            Name = "Test Investment Project",
            FounderKey = "founder-key",
            FounderRecoveryKey = "recovery-key",
            NostrPubKey = "nostr-pub",
            ShortDescription = "Test",
            TargetAmount = 1_000_000,
            StartingDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            EndDate = DateTime.UtcNow.AddYears(1),
            ProjectType = ProjectType.Invest,
            Stages = new List<Stage>()
        };

        _mockProjectService
            .Setup(x => x.GetAsync(projectId))
            .ReturnsAsync(Result.Success(investProject));

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Fund projects");
    }

    [Fact]
    public async Task Handle_WhenAboveThreshold_ReturnsTrue()
    {
        // Arrange
        var projectId = new ProjectId("fund-project");
        var amount = new Amount(2_000_000);
        var request = new CheckPenaltyThreshold.CheckPenaltyThresholdRequest(projectId, amount);

        var fundProject = CreateFundProject(projectId);

        _mockProjectService
            .Setup(x => x.GetAsync(projectId))
            .ReturnsAsync(Result.Success(fundProject));

        _mockInvestorTransactionActions
            .Setup(x => x.IsInvestmentAbovePenaltyThreshold(It.IsAny<ProjectInfo>(), amount.Sats))
            .Returns(true);

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsAboveThreshold.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenBelowThreshold_ReturnsFalse()
    {
        // Arrange
        var projectId = new ProjectId("fund-project");
        var amount = new Amount(500);
        var request = new CheckPenaltyThreshold.CheckPenaltyThresholdRequest(projectId, amount);

        var fundProject = CreateFundProject(projectId);

        _mockProjectService
            .Setup(x => x.GetAsync(projectId))
            .ReturnsAsync(Result.Success(fundProject));

        _mockInvestorTransactionActions
            .Setup(x => x.IsInvestmentAbovePenaltyThreshold(It.IsAny<ProjectInfo>(), amount.Sats))
            .Returns(false);

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsAboveThreshold.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_PassesCorrectAmountToThresholdCheck()
    {
        // Arrange
        var projectId = new ProjectId("fund-project");
        var amount = new Amount(1_500_000);
        var request = new CheckPenaltyThreshold.CheckPenaltyThresholdRequest(projectId, amount);

        var fundProject = CreateFundProject(projectId);

        _mockProjectService
            .Setup(x => x.GetAsync(projectId))
            .ReturnsAsync(Result.Success(fundProject));

        _mockInvestorTransactionActions
            .Setup(x => x.IsInvestmentAbovePenaltyThreshold(It.IsAny<ProjectInfo>(), 1_500_000))
            .Returns(true);

        // Act
        var result = await _sut.Handle(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockInvestorTransactionActions.Verify(
            x => x.IsInvestmentAbovePenaltyThreshold(It.IsAny<ProjectInfo>(), 1_500_000),
            Times.Once);
    }

    private static Project CreateFundProject(ProjectId projectId)
    {
        return new Project
        {
            Id = projectId,
            Name = "Test Fund Project",
            FounderKey = "founder-key",
            FounderRecoveryKey = "recovery-key",
            NostrPubKey = "nostr-pub",
            ShortDescription = "Test fund project",
            TargetAmount = 10_000_000,
            StartingDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            EndDate = DateTime.UtcNow.AddYears(1),
            ProjectType = ProjectType.Fund,
            PenaltyThreshold = 1_000_000,
            Stages = new List<Stage>
            {
                new Stage { RatioOfTotal = 0.5m, ReleaseDate = DateTime.UtcNow.AddMonths(1), Index = 0 },
                new Stage { RatioOfTotal = 0.5m, ReleaseDate = DateTime.UtcNow.AddMonths(2), Index = 1 }
            }
        };
    }
}
