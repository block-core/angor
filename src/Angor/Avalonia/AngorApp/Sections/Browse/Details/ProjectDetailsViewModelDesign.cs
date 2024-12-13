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
        new() { ReleaseDate = DateTimeOffset.Now.Add(TimeSpan.FromDays(60)), Amount = new decimal(0.2), Index = 4, Weight = 0.25d },
    ];

    public ICommand Invest { get; }
}