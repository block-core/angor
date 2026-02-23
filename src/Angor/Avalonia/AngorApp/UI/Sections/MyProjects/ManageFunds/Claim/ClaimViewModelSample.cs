using Angor.Sdk.Funding.Founder.Dtos;
using AngorApp.UI.Shared.Samples;
using AngorApp.UI.Sections.MyProjects.ManageFunds.Claim.Stage;
using Humanizer;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Claim
{
    public class ClaimViewModelSample : IClaimViewModel
    {
        public ClaimViewModelSample()
        {
            Stages = new List<IClaimStage>()
            {
                new ClaimStageSample()
                {
                    StageId = 0,
                    FundsAvailability = FundsAvailability.SpentByFounder,
                    Claim = EnhancedCommand.Create(() => { }, text: "Spent", name: "Spent")
                },
                new ClaimStageSample()
                {
                    StageId = 1,
                    FundsAvailability = FundsAvailability.FundsAvailable,
                    Claim = EnhancedCommand.Create(() => { }, text: "Claim", name: "Available")
                },
                new ClaimStageSample()
                {
                    StageId = 2,
                    FundsAvailability = FundsAvailability.FundsAvailable,
                    Claim = EnhancedCommand.Create(
                        () => { },
                        text: $"Available in {AvailableIn.Humanize()}",
                        name: "NotReady"),
                },
            };
        }

        public IFullProject Project { get; } = new FullProjectSample();

        public IEnumerable<IClaimStage> Stages { get; }

        public TimeSpan AvailableIn { get; set; } = TimeSpan.FromDays(10);
        public IEnhancedCommand<Result<IEnumerable<IClaimStage>>> Load { get; set; } = null!;
        public FundsAvailability FundsAvailability { get; set; }
    }
}