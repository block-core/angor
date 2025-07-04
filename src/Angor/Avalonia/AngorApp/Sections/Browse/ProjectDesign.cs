using Angor.UI.Model;

namespace AngorApp.Sections.Browse;


public class ProjectDesign : IProject
{
    public string Id { get; set; } = "angor1qmd8kazm8uzk7s0gddf4amjx4mzj3n5wzgn3mde";
    public string Name { get; set; } = "Test Project";
    public Uri Banner { get; set; } = new Uri("https://pbs.twimg.com/profile_images/1835739941596430336/0zk73Tpj_400x400.png");
    public Uri Picture { get; set; } = new Uri("https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcSskvatKL_o8y2E2Zvaj8KEEYyyEzcJS6frydUCPizL_Txe1j3CqgjVq_WIfhEcM64IL0I&usqp=CAU");
    public string ShortDescription { get; set; } = "Short description of the project";
    public string BitcoinAddress { get; } = "some address";
    public decimal TargetAmount { get; } = 50m;
    public DateTime StartingDate { get; } = DateTime.Now;

    public IEnumerable<IStage> Stages { get; } =
    [
        new StageDesign() { ReleaseDate = DateTime.Today, Amount = 1000_000, Index = 1, RatioOfTotal = 0.25d },
        new StageDesign() { ReleaseDate = DateTime.Today.AddDays(20), Amount = 400_0000, Index = 2, RatioOfTotal = 0.25d },
        new StageDesign() { ReleaseDate = DateTime.Today.AddDays(40), Amount = 800_0000, Index = 3, RatioOfTotal = 0.25d },
        new StageDesign() { ReleaseDate = DateTime.Today.AddDays(60), Amount = 1000_0000, Index = 4, RatioOfTotal = 0.25d }
    ];

    public string NpubKey { get; } = "npub17a0glwdvr5wjyjdh5eu4xmh4swtaqrmhcgss22unvr6p3spyyq7qeh4kaz";
    public string NpubKeyHex { get; } = "f75e8fb9ac1d1d2249b7a679536ef58397d00f77c221052b9360f418c024203c";
    public TimeSpan PenaltyDuration { get; } = TimeSpan.FromDays(90);
    public Uri InformationUri { get; } = new Uri("https://www.google.com");

    public override string ToString() => Name;
}