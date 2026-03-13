using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Dtos;
using Angor.Sdk.Funding.Shared;
using AngorApp.Model.ProjectsV2.InvestmentProject;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Zafiro.UI.Commands;

namespace AngorApp.Tests.UI.Sections.MyProjects.ManageFunds;

public class ManageFundsProjectStatisticsTests
{
    [Fact]
    public async Task InvestmentProject_maps_statistics_from_project_statistics_dto()
    {
        var dto = new ProjectStatisticsDto
        {
            TotalInvested = 120_000_000,
            AvailableBalance = 80_000_000,
            WithdrawableAmount = 50_000_000,
            TotalStages = 7
        };

        Mock<IProjectAppService> projectAppService = new();
        projectAppService
            .Setup(x => x.GetProjectStatistics(It.IsAny<ProjectId>()))
            .ReturnsAsync(Result.Success(dto));

        var seed = new ProjectDto
        {
            Id = new ProjectId("project-1"),
            Name = "Project",
            ShortDescription = "Desc",
            TargetAmount = 200_000_000,
            FundingStartDate = DateTime.UtcNow.AddDays(-1),
            FundingEndDate = DateTime.UtcNow.AddDays(30),
            PenaltyDuration = TimeSpan.FromDays(1),
            NostrNpubKeyHex = "npub",
            Stages = [],
            FounderPubKey = "founder"
        };

        var invest = new Mock<IEnhancedCommand<Result>>().Object;
        var sut = new InvestmentProject(seed, projectAppService.Object, invest);

        var totalInvestmentTask = sut.TotalInvestment.FirstAsync().ToTask();
        var availableBalanceTask = sut.AvailableBalance.FirstAsync().ToTask();
        var withdrawableTask = sut.Withdrawable.FirstAsync().ToTask();
        var totalStagesTask = sut.TotalStages.FirstAsync().ToTask();

        sut.Refresh.Execute(null);

        (await totalInvestmentTask.WaitAsync(TimeSpan.FromSeconds(1))).Sats.Should().Be(dto.TotalInvested);
        (await availableBalanceTask.WaitAsync(TimeSpan.FromSeconds(1))).Sats.Should().Be(dto.AvailableBalance);
        (await withdrawableTask.WaitAsync(TimeSpan.FromSeconds(1))).Sats.Should().Be(dto.WithdrawableAmount);
        (await totalStagesTask.WaitAsync(TimeSpan.FromSeconds(1))).Should().Be(dto.TotalStages);
    }
}
