namespace AngorApp.Sections.Browse;

public interface IProject
{
    public string Id { get; set; }
    public string Name { get; }
    public Uri Picture { get; }
    public Uri Icon { get; }
    public string ShortDescription { get; }
    string BitcoinAddress { get; }
}

public class ProjectDesign : IProject
{
    public string Id { get; set; } = "angor1qmd8kazm8uzk7s0gddf4amjx4mzj3n5wzgn3mde";
    public string Name { get; set; }
    public Uri Picture { get; set; }
    public Uri Icon { get; set; }
    public string ShortDescription { get; set; }
    public string BitcoinAddress { get; }
}