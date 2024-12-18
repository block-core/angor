namespace AngorApp.Sections.Browse;

public class Project(string name) : IProject
{
    public string Id { get; set; }
    public string Name { get; } = name;
    public Uri Picture { get; init; }
    public Uri Icon { get; set; }
    public string ShortDescription { get; set; }
    
    public string BitcoinAddress { get; }

    public override string ToString()
    {
        return Name;
    }
}