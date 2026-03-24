using System.Reactive;
using System.Reactive.Linq;
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
                SelectedDurationValue = 3,
                SelectedDurationUnit = PeriodUnit.Months,
                ReleaseFrequency = new PeriodOption { Title = "Monthly", Unit = PeriodUnit.Months, Value = 1 }
            };

            sut.GenerateStages.Execute(Unit.Default).Subscribe();

            newProject.Stages.Select(stage => stage.Percent).Should().Equal(0.33m, 0.33m, 0.34m);
        }

        [Fact]
        public void DurationPresets_change_with_selected_duration_unit()
        {
            using InvestmentProjectConfig newProject = new();
            StagesViewModel sut = new(newProject);

            sut.SelectedDurationUnit = PeriodUnit.Weeks;

            sut.DurationPresets.FirstAsync().Wait().Select(option => option.Value).Should().Equal(2, 4, 6, 8, 12);
        }

        [Fact]
        public void Selecting_duration_preset_keeps_selected_duration_unit()
        {
            using InvestmentProjectConfig newProject = new();
            StagesViewModel sut = new(newProject)
            {
                SelectedDurationUnit = PeriodUnit.Weeks
            };

            var selectedPreset = sut.DurationPresets.FirstAsync().Wait().Single(option => option.Value == 6);
            sut.SelectedLength = selectedPreset;

            sut.SelectedDurationValue.Should().Be(6);
            sut.SelectedDurationUnit.Should().Be(PeriodUnit.Weeks);
        }

        [Fact]
        public void Editing_duration_value_selects_matching_preset_with_same_unit()
        {
            using InvestmentProjectConfig newProject = new();
            StagesViewModel sut = new(newProject)
            {
                SelectedDurationUnit = PeriodUnit.Months
            };

            sut.SelectedDurationValue = 6;

            sut.SelectedLength.Should().Be(new PeriodOption
            {
                Title = "6 Months",
                Value = 6,
                Unit = PeriodUnit.Months
            });
        }

        [Fact]
        public void Presets_with_same_value_but_different_unit_are_not_equal()
        {
            new PeriodOption { Value = 6, Unit = PeriodUnit.Months, Title = "6 Months" }
                .Should()
                .NotBe(new PeriodOption { Value = 6, Unit = PeriodUnit.Days, Title = "6 Days" });
        }

        [Fact]
        public void GenerateStages_uses_calendar_months_for_monthly_frequencies()
        {
            using InvestmentProjectConfig newProject = new()
            {
                FundingEndDate = new DateTime(2026, 1, 31),
            };
            StagesViewModel sut = new(newProject)
            {
                SelectedDurationValue = 12,
                SelectedDurationUnit = PeriodUnit.Months,
                ReleaseFrequency = new PeriodOption { Title = "Quarterly", Unit = PeriodUnit.Months, Value = 3 }
            };

            sut.GenerateStages.Execute(Unit.Default).Subscribe();

            newProject.Stages.Select(stage => stage.ReleaseDate!.Value.Date).Should().Equal(
                new DateTime(2026, 4, 30),
                new DateTime(2026, 7, 31),
                new DateTime(2026, 10, 31),
                new DateTime(2027, 1, 31));
        }
    }
}
