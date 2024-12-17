namespace AngorApp.Sections.Browse;

public interface IProject
{
    public string Name { get; }
    public Uri Picture { get; }
    public Uri Icon { get; }
    public string ShortDescription { get; }
    string Address { get; }
}

public class ProjectDesign : IProject
{
    public Uri? Uri { get; set; }
    public string Name { get; set; }
    public Uri Picture { get; set; }
    public Uri Icon { get; set; }
    public string ShortDescription { get; set; }
    public string Address { get; }
}