using System.Reactive;
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
        public void GenerateStages_uses_integer_percentages_summing_to_100()
        {
            using InvestmentProjectConfig newProject = new() { FundingEndDate = DateTime.Now.AddDays(1) };
            StagesViewModel sut = new(newProject)
            {
                DurationValue = 3,
                DurationUnit = TimeSpan.FromDays(30),
                ReleaseFrequency = TimeSpan.FromDays(30)
            };

            sut.GenerateStages.Execute(Unit.Default).Subscribe();

            var percents = newProject.Stages.Select(s => s.Percent ?? 0).ToList();
            percents.Sum().Should().Be(100m);
            percents.Should().OnlyContain(p => p == Math.Floor(p), "all percentages should be whole integers");
        }

        [Fact]
        public void GenerateStages_distributes_3_stages_as_33_33_34()
        {
            using InvestmentProjectConfig newProject = new() { FundingEndDate = DateTime.Now.AddDays(1) };
            StagesViewModel sut = new(newProject)
            {
                DurationValue = 3,
                DurationUnit = TimeSpan.FromDays(30),
                ReleaseFrequency = TimeSpan.FromDays(30)
            };

            sut.GenerateStages.Execute(Unit.Default).Subscribe();

            var percents = newProject.Stages.Select(s => s.Percent ?? 0).ToList();
            percents.Should().HaveCount(3);
            percents.Sum().Should().Be(100m);
            percents.Should().Contain(34m, "remainder stage should get the extra 1%");
            percents.Count(p => p == 33m).Should().Be(2);
        }

        [Fact]
        public void GenerateStages_distributes_4_stages_evenly_as_25_each()
        {
            using InvestmentProjectConfig newProject = new() { FundingEndDate = DateTime.Now.AddDays(1) };
            StagesViewModel sut = new(newProject)
            {
                DurationValue = 4,
                DurationUnit = TimeSpan.FromDays(30),
                ReleaseFrequency = TimeSpan.FromDays(30)
            };

            sut.GenerateStages.Execute(Unit.Default).Subscribe();

            var percents = newProject.Stages.Select(s => s.Percent ?? 0).ToList();
            percents.Should().HaveCount(4);
            percents.Should().AllBeEquivalentTo(25m);
        }
    }
}