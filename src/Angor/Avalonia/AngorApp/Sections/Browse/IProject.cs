namespace AngorApp.Sections.Browse;

public interface IProject
{
    public string Id { get; set; }
    public string Name { get; }
    public Uri Picture { get; }
    public Uri Icon { get; }
    public string ShortDescription { get; }
    string BitcoinAddress { get; }
    public decimal TargetAmount { get; }
    public DateOnly StartingDate { get; }
    IEnumerable<IStage> Stages { get; }
    public string NpubKey { get; }
    public string NpubKeyHex { get; }
    
}

public class ProjectDesign : IProject
{
    public string Id { get; set; } = "angor1qmd8kazm8uzk7s0gddf4amjx4mzj3n5wzgn3mde";
    public string Name { get; set; } = "Test Project";
    public Uri Picture { get; set; } = new Uri("https://images.pexels.com/photos/998641/pexels-photo-998641.jpeg?auto=compress&cs=tinysrgb&w=600");
    public Uri Icon { get; set; } = new Uri("https://images.pexels.com/photos/998641/pexels-photo-998641.jpeg?auto=compress&cs=tinysrgb&w=600");
    public string ShortDescription { get; set; } = "Short description of the project";
    public string BitcoinAddress { get; } = "some address";
    public decimal TargetAmount { get; } = 50m;
    public DateOnly StartingDate { get; } = DateOnly.FromDateTime(DateTime.Now);

    public IEnumerable<IStage> Stages { get; } =
    [
        new StageDesign() { ReleaseDate = DateOnly.FromDateTime(DateTime.Today), Amount = new decimal(0.1), Index = 1, Weight = 0.25d },
        new StageDesign() { ReleaseDate = DateOnly.FromDateTime(DateTime.Today).AddDays(20), Amount = new decimal(0.4), Index = 2, Weight = 0.25d },
        new StageDesign() { ReleaseDate = DateOnly.FromDateTime(DateTime.Today).AddDays(40), Amount = new decimal(0.3), Index = 3, Weight = 0.25d },
        new StageDesign() { ReleaseDate = DateOnly.FromDateTime(DateTime.Today).AddDays(60), Amount = new decimal(0.2), Index = 4, Weight = 0.25d }
    ];

    public string NpubKey { get; } = "npub17a0glwdvr5wjyjdh5eu4xmh4swtaqrmhcgss22unvr6p3spyyq7qeh4kaz";
    public string NpubKeyHex { get; } = "f75e8fb9ac1d1d2249b7a679536ef58397d00f77c221052b9360f418c024203c";

    public override string ToString() => Name;
}

public class StageDesign : IStage
{
    public DateOnly ReleaseDate { get; set; }
    public decimal Amount { get; set; }
    public int Index { get; set; }
    public double Weight { get; set; }
}

public interface IStage
{
    DateOnly ReleaseDate { get; }
    decimal Amount { get; }
    int Index { get; }
    double Weight { get; }
}