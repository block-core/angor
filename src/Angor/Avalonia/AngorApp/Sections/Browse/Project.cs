namespace AngorApp.Sections.Browse;

public class Project(string name)
{
    public string Name { get; } = name;
    public Uri Picture { get; init; }
    public Uri Icon { get; set; }
    public string ShortDescription { get; set; }
    public string Address { get; set; }

    public override string ToString()
    {
        return Name;
    }
}