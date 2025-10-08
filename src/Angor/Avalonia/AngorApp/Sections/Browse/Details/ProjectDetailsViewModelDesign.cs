using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.UI.Model.Implementation.Projects;
using AngorApp.UI.Controls.Common.FoundedProjectOptions;

namespace AngorApp.Sections.Browse.Details;

public class ProjectDetailsViewModelDesign : IProjectDetailsViewModel
{
    public ProjectDetailsViewModelDesign()
    {
        Picture = new Uri("https://images.pexels.com/photos/998641/pexels-photo-998641.jpeg?auto=compress&cs=tinysrgb&w=600");
        Icon = new Uri("https://i.nostr.build/3bjqfqHOOBFWckk7.png");
    }

    public object Icon { get; }
    public object Picture { get; }

    public IEnhancedCommand<Result<Maybe<Unit>>> Invest { get; }

    public IEnumerable<INostrRelay> Relays { get; } =
    [
        new NostrRelayDesign
        {
            Uri = new Uri("wss://relay.angor.io")
        },
        new NostrRelayDesign
        {
            Uri = new Uri("wss://relay2.angor.io")
        }
    ];

    public IFullProject Project { get; set; } = new FullProjectDesign();
    public bool IsInsideInvestmentPeriod { get; set; } = true;
    public TimeSpan? NextRelease { get; } = TimeSpan.FromDays(15);
    public IStage? CurrentStage { get; } = new StageDesign();
    public IFoundedProjectOptionsViewModel FoundedProjectOptions { get; } = new FoundedProjectOptionsViewModelDesign();
}