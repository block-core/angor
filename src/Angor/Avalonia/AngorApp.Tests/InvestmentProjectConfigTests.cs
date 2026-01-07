using System.Reactive.Linq;
using AngorApp.Model.Amounts;
using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject.Model;
using AngorApp.UI.Shared.Services;
using FluentAssertions;
using Moq;
using ReactiveUI.Validation.Extensions;

namespace AngorApp.Tests
{
    public class InvestmentProjectConfigTests
    {
        public InvestmentProjectConfigTests()
        {
            Mock<IUIServices> uiServicesMock1 = new();
            uiServicesMock1.Setup(x => x.EnableProductionValidations()).Returns(false);
        }

        private IFundingStageConfig AddStage(IInvestmentProjectConfig project, decimal percent)
        {
            return project.CreateAndAddStage(percent);
        }

        [Fact]
        public async Task Validation_fails_when_sum_is_not_100()
        {
            using InvestmentProjectConfig sut = new()
            {
                TargetAmount = new MutableAmountUI { Sats = 100000 },
                PenaltyDays = 10,
                FundingEndDate = DateTime.Now.AddDays(1),
                StartDate = DateTime.Now
            };
            AddStage(sut, 0.5m).ReleaseDate = DateTime.Now.AddDays(10);
            AddStage(sut, 0.4m).ReleaseDate = DateTime.Now.AddDays(20);

            bool isValid = await sut.IsValid().FirstAsync();
            isValid.Should().BeFalse();
        }

        [Fact]
        public async Task Validation_passes_when_all_valid()
        {
            using InvestmentProjectConfig sut = new()
            {
                Name = "My project",
                Description = "My description",
                TargetAmount = new MutableAmountUI { Sats = 100000 },
                PenaltyDays = 10,
                FundingEndDate = DateTime.Now.AddDays(1),
                StartDate = DateTime.Now
            };
            AddStage(sut, 0.5m).ReleaseDate = DateTime.Now.AddDays(10);
            AddStage(sut, 0.5m).ReleaseDate = DateTime.Now.AddDays(20);

            bool isValid = await sut.IsValid().FirstAsync();
            isValid.Should().BeTrue();
        }

        [Fact]
        public async Task Validation_fails_when_Name_is_missing()
        {
            using InvestmentProjectConfig sut = new()
            {
                // Name missing
                Description = "My description",
                TargetAmount = new MutableAmountUI { Sats = 100000 },
                PenaltyDays = 10,
                FundingEndDate = DateTime.Now.AddDays(1),
                StartDate = DateTime.Now
            };
            AddStage(sut, 0.5m).ReleaseDate = DateTime.Now.AddDays(10);
            AddStage(sut, 0.5m).ReleaseDate = DateTime.Now.AddDays(20);

            bool isValid = await sut.IsValid().FirstAsync();
            isValid.Should().BeFalse();
        }

        [Fact]
        public async Task Validation_fails_when_Description_is_missing()
        {
            using InvestmentProjectConfig sut = new()
            {
                Name = "My project",
                Description = "", // Description missing
                TargetAmount = new MutableAmountUI { Sats = 100000 },
                PenaltyDays = 10,
                FundingEndDate = DateTime.Now.AddDays(1),
                StartDate = DateTime.Now
            };
            AddStage(sut, 0.5m).ReleaseDate = DateTime.Now.AddDays(10);
            AddStage(sut, 0.5m).ReleaseDate = DateTime.Now.AddDays(20);

            bool isValid = await sut.IsValid().FirstAsync();
            isValid.Should().BeFalse();
        }

        [Fact]
        public async Task Validation_fails_when_Website_is_invalid()
        {
            using InvestmentProjectConfig sut = new()
            {
                Name = "My project",
                Description = "My description",
                Website = "invalid-url", // Invalid
                TargetAmount = new MutableAmountUI { Sats = 100000 },
                PenaltyDays = 10,
                FundingEndDate = DateTime.Now.AddDays(1),
                StartDate = DateTime.Now
            };
            AddStage(sut, 0.5m).ReleaseDate = DateTime.Now.AddDays(10);
            AddStage(sut, 0.5m).ReleaseDate = DateTime.Now.AddDays(20);

            bool isValid = await sut.IsValid().FirstAsync();
            isValid.Should().BeFalse();
        }

        [Fact]
        public async Task Validation_passes_when_Website_is_valid()
        {
            using InvestmentProjectConfig sut = new()
            {
                Name = "My project",
                Description = "My description",
                Website = "https://example.com", // Valid
                TargetAmount = new MutableAmountUI { Sats = 100000 },
                PenaltyDays = 10,
                FundingEndDate = DateTime.Now.AddDays(1),
                StartDate = DateTime.Now
            };
            AddStage(sut, 0.5m).ReleaseDate = DateTime.Now.AddDays(10);
            AddStage(sut, 0.5m).ReleaseDate = DateTime.Now.AddDays(20);

            bool isValid = await sut.IsValid().FirstAsync();
            isValid.Should().BeTrue();
        }

        [Fact]
        public async Task Validation_passes_when_Website_is_empty()
        {
            using InvestmentProjectConfig sut = new()
            {
                Name = "My project",
                Description = "My description",
                Website = "", // Empty is valid (optional)
                TargetAmount = new MutableAmountUI { Sats = 100000 },
                PenaltyDays = 10,
                FundingEndDate = DateTime.Now.AddDays(1),
                StartDate = DateTime.Now
            };
            AddStage(sut, 0.5m).ReleaseDate = DateTime.Now.AddDays(10);
            AddStage(sut, 0.5m).ReleaseDate = DateTime.Now.AddDays(20);

            bool isValid = await sut.IsValid().FirstAsync();
            isValid.Should().BeTrue();
        }

        [Fact]
        public async Task Validation_updates_dynamically()
        {
            using InvestmentProjectConfig sut = new()
            {
                Name = "My project",
                Description = "My description",
                TargetAmount = new MutableAmountUI { Sats = 100000 },
                PenaltyDays = 10,
                FundingEndDate = DateTime.Now.AddDays(1),
                StartDate = DateTime.Now
            };
            IFundingStageConfig stage1 = AddStage(sut, 0.5m);
            stage1.ReleaseDate = DateTime.Now.AddDays(10);
            IFundingStageConfig stage2 = AddStage(sut, 0.4m);
            stage2.ReleaseDate = DateTime.Now.AddDays(20);

            bool isValidInitial = await sut.IsValid().FirstAsync();
            isValidInitial.Should().BeFalse();

            stage1.Percent = 0.6m;

            // Wait for the validation to become true
            bool isValidAfterUpdate = await sut.IsValid()
                                               .Where(valid => valid)
                                               .FirstAsync()
                                               .Timeout(TimeSpan.FromSeconds(1));

            isValidAfterUpdate.Should().BeTrue();
        }

        [Fact]
        public async Task Validation_fails_when_TargetAmount_invalid()
        {
            using InvestmentProjectConfig sut = new()
            {
                TargetAmount = new MutableAmountUI { Sats = 0 }, // Invalid
                PenaltyDays = 10,
                FundingEndDate = DateTime.Now.AddDays(1),
                StartDate = DateTime.Now
            };
            // Stages valid
            AddStage(sut, 0.5m).ReleaseDate = DateTime.Now.AddDays(10);
            AddStage(sut, 0.5m).ReleaseDate = DateTime.Now.AddDays(20);

            bool isValid = await sut.IsValid().FirstAsync();
            isValid.Should().BeFalse();
        }

        [Fact]
        public async Task Validation_fails_when_PenaltyDays_invalid()
        {
            using InvestmentProjectConfig sut = new()
            {
                TargetAmount = new MutableAmountUI { Sats = 100000 },
                PenaltyDays = -1, // Invalid
                FundingEndDate = DateTime.Now.AddDays(1),
                StartDate = DateTime.Now
            };
            // Stages valid
            AddStage(sut, 0.5m).ReleaseDate = DateTime.Now.AddDays(10);
            AddStage(sut, 0.5m).ReleaseDate = DateTime.Now.AddDays(20);

            bool isValid = await sut.IsValid().FirstAsync();
            isValid.Should().BeFalse();
        }
    }
}