using System.Linq;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder;

public class FounderSectionViewModelDesign : IFounderSectionViewModel
{
    public IEnhancedCommand<Unit, Result<IEnumerable<ProjectDto>>> LoadProjects { get; } = ReactiveCommand.Create(() => Result.Success(Enumerable.Empty<ProjectDto>())).Enhance();

    public IEnumerable<IFounderProjectViewModel> Projects { get; } = new List<IFounderProjectViewModel>()
    {
        new FounderProjectViewModelDesign()
        {
            Name = "First project",
            Picture = new Uri("https://theunpluggednetwork.com/wp-content/uploads/2025/03/App-Testimonial-5-600x152.jpg"),
            Banner = new Uri("https://images-assets.nasa.gov/image/PIA05062/PIA05062~thumb.jpg"),
            ShortDescription = "Sample project with long description bla bla blah blah bla blah bla bla blah blah bla blah bla bla blah blah bla blah bla bla blah blah bla blah bla bla blah blah bla blah bla bla blah blah bla blah bla bla blah blah bla blah",
            TargetAmount = 1234,
        },
        new FounderProjectViewModelDesign()
        {
            Name = "Second project",
            Picture = new Uri("https://theunpluggednetwork.com/wp-content/uploads/2025/03/App-Testimonial-5-600x152.jpg"),
            Banner = new Uri("https://images-assets.nasa.gov/image/PIA05062/PIA05062~thumb.jpg"),
            ShortDescription = "Sample project",
            TargetAmount = 1234,
        },
        new FounderProjectViewModelDesign()
        {
            Name = "Third project",
            Picture = new Uri("https://theunpluggednetwork.com/wp-content/uploads/2025/03/App-Testimonial-5-600x152.jpg"),
            Banner = new Uri("https://images-assets.nasa.gov/image/PIA05062/PIA05062~thumb.jpg"),
            ShortDescription = "Sample project",
            TargetAmount = 1234,
        }
    };
}