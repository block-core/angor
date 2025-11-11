using Angor.Contexts.Funding.Projects.Application.Dtos;
using AngorApp.Model.Projects;
using AngorApp.UI.Controls.Common.FoundedProjectOptions;

namespace AngorApp.Sections.Browse.Details;

public class ProjectDetailsViewModelSample : IProjectDetailsViewModel
{
    public ProjectDetailsViewModelSample()
    {
        Picture = new Uri("https://images.pexels.com/photos/998641/pexels-photo-998641.jpeg?auto=compress&cs=tinysrgb&w=600");
        Icon = new Uri("https://i.nostr.build/3bjqfqHOOBFWckk7.png");
    }

    public object Icon { get; }
    public object Picture { get; }

    public IEnhancedCommand<Result<Maybe<Unit>>> Invest { get; }

    public IEnumerable<INostrRelay> Relays { get; } =
    [
        new NostrRelaySample
        {
            Uri = new Uri("wss://relay.angor.io")
        },
        new NostrRelaySample
        {
            Uri = new Uri("wss://relay2.angor.io")
        }
    ];

    public IFullProject Project { get; set; } = new FullProjectSample();
    public bool IsInsideInvestmentPeriod { get; set; } = true;
    public TimeSpan? NextRelease { get; } = TimeSpan.FromDays(15);
    public IStage? CurrentStage { get; } = new StageSample();
    public IFoundedProjectOptionsViewModel FoundedProjectOptions { get; } = new FoundedProjectOptionsViewModelSample();
}