using System.Reactive.Linq;
using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model;
using FluentAssertions;

namespace AngorApp.Tests
{
    public class FundingStageConfigTests
    {
        [Fact]
        public void Percent_clamps_values_upper_bound()
        {
            FundingStageConfig stage = new();
            stage.Percent = 1.5m;
            stage.Percent.Should().Be(1.0m);
        }

        [Fact]
        public void Percent_clamps_values_lower_bound()
        {
            FundingStageConfig stage = new();
            stage.Percent = -1.0m;
            stage.Percent.Should().Be(0.0m);
        }

        [Fact]
        public void Validation_fails_when_Percent_is_null()
        {
            FundingStageConfig stage = new();
            stage.Percent = null;
            stage.IsValid.FirstAsync().Wait().Should().BeFalse();
        }
    }
}