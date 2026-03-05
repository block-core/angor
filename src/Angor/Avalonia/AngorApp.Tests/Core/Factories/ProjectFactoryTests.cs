using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Dtos;
using Angor.Sdk.Funding.Shared;
using Angor.Shared.Models;
using AngorApp.Core.Factories;
using AngorApp.Model.ProjectsV2.FundProject;
using AngorApp.Model.ProjectsV2.InvestmentProject;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Investment;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using Zafiro.UI.Commands;
using Zafiro.UI.Navigation;
using FundManageFunds = AngorApp.UI.Sections.MyProjects.ManageFunds.Fund;

namespace AngorApp.Tests.Core.Factories;

public class ProjectFactoryTests
{
    [Fact]
    public void Create_returns_investment_project_with_enabled_manage_funds_for_invest_type()
    {
        var sut = CreateSut();

        var result = sut.Create(CreateSeed(ProjectType.Invest));

        result.Should().BeAssignableTo<IInvestmentProject>();
        result.ManageFunds.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void Create_returns_fund_project_with_enabled_manage_funds_for_fund_type()
    {
        var sut = CreateSut();

        var result = sut.Create(CreateSeed(ProjectType.Fund));

        result.Should().BeAssignableTo<IFundProject>();
        result.ManageFunds.CanExecute(null).Should().BeTrue();
    }

    private static IProjectFactory CreateSut()
    {
        Mock<IProjectAppService> projectAppService = new();
        Mock<IProjectInvestCommandFactory> projectInvestCommandFactory = new();
        Mock<INavigator> navigator = new();
        var manageFundsFactory = new Mock<Func<IInvestmentProject, IManageFundsViewModel>>();
        var fundManageFundsFactory = new Mock<Func<IFundProject, FundManageFunds.IManageFundsViewModel>>();

        projectInvestCommandFactory
            .Setup(x => x.Create(It.IsAny<ProjectId>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<ProjectType>()))
            .Returns(EnhancedCommand.CreateWithResult(Result.Success));

        return new ProjectFactory(projectAppService.Object, projectInvestCommandFactory.Object, manageFundsFactory.Object, fundManageFundsFactory.Object, navigator.Object);
    }

    private static ProjectDto CreateSeed(ProjectType projectType)
    {
        return new ProjectDto
        {
            Id = new ProjectId($"project-{projectType}"),
            Name = "Project",
            ShortDescription = "Desc",
            TargetAmount = 200_000_000,
            FundingStartDate = DateTime.UtcNow.AddDays(-1),
            FundingEndDate = DateTime.UtcNow.AddDays(30),
            PenaltyDuration = TimeSpan.FromDays(1),
            NostrNpubKeyHex = "npub",
            FounderPubKey = "founder",
            Stages = [],
            ProjectType = projectType
        };
    }
}
