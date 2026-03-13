using System.Reactive;
using System.Linq;
using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model;
using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Stages;
using AngorApp.UI.Shared.Services;
using DynamicData;
using FluentAssertions;
using Moq;

namespace AngorApp.Tests
{
    public class StagesViewModelTests
    {
        private readonly Mock<IUIServices> uiServicesMock;

        public StagesViewModelTests()
        {
            uiServicesMock = new Mock<IUIServices>();
            uiServicesMock.Setup(x => x.EnableProductionValidations()).Returns(false);
        }

        [Fact]
        public void AddStage_adds_new_stage_to_project()
        {
            using InvestmentProjectConfig newProject = new();
            StagesViewModel sut = new(newProject);

            sut.AddStage.Execute(Unit.Default).Subscribe();

            newProject.Stages.Should().HaveCount(1);
        }

        [Fact]
        public void RemoveStage_removes_stage_from_project()
        {
            using InvestmentProjectConfig newProject = new();
            FundingStageConfig stage = new();
            newProject.StagesSource.Add(stage);
            StagesViewModel sut = new(newProject);

            sut.RemoveStage.Execute(stage).Subscribe();

            newProject.Stages.Should().BeEmpty();
        }

        [Fact]
        public void GenerateStages_uses_whole_percentages_that_sum_to_100()
        {
            using InvestmentProjectConfig newProject = new()
            {
                FundingEndDate = DateTime.Today,
            };
            StagesViewModel sut = new(newProject)
            {
                DurationValue = 3,
                DurationUnit = TimeSpan.FromDays(30),
                ReleaseFrequency = TimeSpan.FromDays(30)
            };

            sut.GenerateStages.Execute(Unit.Default).Subscribe();

            newProject.Stages.Select(stage => stage.Percent).Should().Equal(0.33m, 0.33m, 0.34m);
        }
    }
}
