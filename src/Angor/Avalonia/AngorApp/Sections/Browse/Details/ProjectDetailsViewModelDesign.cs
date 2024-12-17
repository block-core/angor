using System.Windows.Input;

namespace AngorApp.Sections.Browse.Details;

public class ProjectDetailsViewModelDesign : IProjectDetailsViewModel
{
    public ProjectDetailsViewModelDesign()
    {
        Picture = new Uri("https://images.pexels.com/photos/998641/pexels-photo-998641.jpeg?auto=compress&cs=tinysrgb&w=600");
        Icon = new Uri("https://images.pexels.com/photos/998641/pexels-photo-998641.jpeg?auto=compress&cs=tinysrgb&w=600");
    }

    public string Name { get; } = "Test Project";
    public string ShortDescription { get; } = "Test Project";
    public object Icon { get; }
    public object Picture { get; }

    public IEnumerable<Stage> Stages { get; } =
    [
        new() { ReleaseDate = DateTimeOffset.Now.Date.AddDays(1), Amount = new decimal(0.1), Index = 1, Weight = 0.25d },
        new() { ReleaseDate = DateTimeOffset.Now.Add(TimeSpan.FromDays(20)), Amount = new decimal(0.4), Index = 2, Weight = 0.25d },
        new() { ReleaseDate = DateTimeOffset.Now.Add(TimeSpan.FromDays(40)), Amount = new decimal(0.3), Index = 3, Weight = 0.25d },
        new() { ReleaseDate = DateTimeOffset.Now.Add(TimeSpan.FromDays(60)), Amount = new decimal(0.2), Index = 4, Weight = 0.25d }
    ];

    public ICommand Invest { get; }
    public string NpubKey { get; } = "npub109t62lkxkfs7m4cac0lp0en45ndl3kdcnqm0serd450dravj9lvq3duh5k";
    public string NpubKeyHex { get; } = "7957a57ec6b261edd71dc3fe17e675a4dbf8d9b89836f8646dad1ed1f5922fd8";

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
}

public class NostrRelayDesign : INostrRelay
{
    public Uri Uri { get; set; }
}

public interface INostrRelay
{
    public Uri Uri { get; }
}