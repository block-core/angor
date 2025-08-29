using Angor.Contexts.Funding.Projects.Application.Dtos;
using Angor.UI.Model.Implementation.Projects;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Browse.Details;

public class ProjectDetailsViewModelDesign : IProjectDetailsViewModel
{
    public ProjectDetailsViewModelDesign()
    {
        Picture = new Uri("https://images.pexels.com/photos/998641/pexels-photo-998641.jpeg?auto=compress&cs=tinysrgb&w=600");
        Icon = new Uri("https://images.pexels.com/photos/998641/pexels-photo-998641.jpeg?auto=compress&cs=tinysrgb&w=600");
    }

    public object Icon { get; }
    public object Picture { get; }

    public IEnhancedCommand<Result> Invest { get; }

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

    public TimeSpan TimeFromFundingStartingDate { get; } = TimeSpan.FromDays(12);
    public IAmountUI TargetAmount { get; } = new AmountUI(200);
    public TimeSpan TimeToFundingEndDate { get; } = TimeSpan.FromDays(90);
    public double CurrentDays { get; } = 11;
    public IAmountUI RaisedAmount { get; } = new AmountUI(100);
    public IFullProject Project { get; set; } = new FullProjectDesign();
    public bool IsInsideInvestmentPeriod { get; set; } = true;
    public TimeSpan? NextRelease { get; } = TimeSpan.FromDays(15);
    public IStage? CurrentStage { get; } = new StageDesign();
    public TimeSpan FundingPeriod => TimeSpan.FromDays(78);
    public TimeSpan PenaltyDuration { get; } = TimeSpan.FromDays(90);
}