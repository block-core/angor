using AngorApp.Sections.Browse.Details;

namespace AngorApp;

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
}