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
    }
}