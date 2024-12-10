namespace AngorApp.Sections.Browse;

public interface IProject
{
    public string Name { get; }
    public object Picture { get; }
    public object Icon { get; }
    public string ShortDescription { get; }
}

public class ProjectDesign : IProject
{
    public Uri? Uri { get; set; }
    public string Name { get; set; }
    public object Picture { get; set; }
    public object Icon { get; set; }
    public string ShortDescription { get; set; }
}