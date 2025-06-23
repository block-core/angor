using System.Windows.Input;
using Angor.UI.Model;
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

    public double TotalDays { get; } = 119;
    public double TotalInvestment { get; } = 1.5d;
    public double CurrentDays { get; } = 11;
    public double CurrentInvestment { get; } = 0.79d;
    public IProject Project { get; set; } = new ProjectDesign();
}