using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ReactiveUI;
using Zafiro.CSharpFunctionalExtensions;

namespace AngorApp.UI.Sections.Founder;

public class FounderSectionViewModelSample : IFounderSectionViewModel
{
    public FounderSectionViewModelSample()
    {
        var projects = new ObservableCollection<IFounderProjectViewModel>
        {
            new FounderProjectViewModelSample
            {
                Name = "First project",
                Picture = new Uri("https://theunpluggednetwork.com/wp-content/uploads/2025/03/App-Testimonial-5-600x152.jpg"),
                Banner = new Uri("https://images-assets.nasa.gov/image/PIA05062/PIA05062~thumb.jpg"),
                ShortDescription = "Sample project with long description bla bla blah blah bla blah bla bla blah blah bla blah bla bla blah blah bla blah bla bla blah blah bla blah bla bla blah blah bla blah bla bla blah blah bla blah",
                TargetAmount = 1234,
            },
            new FounderProjectViewModelSample
            {
                Name = "Second project",
                Picture = new Uri("https://theunpluggednetwork.com/wp-content/uploads/2025/03/App-Testimonial-5-600x152.jpg"),
                Banner = new Uri("https://images-assets.nasa.gov/image/PIA14417/PIA14417~thumb.jpg"),
                ShortDescription = "Sample project",
                TargetAmount = 1234,
            },
            new FounderProjectViewModelSample
            {
                Name = "Third project",
                Picture = new Uri("https://theunpluggednetwork.com/wp-content/uploads/2025/03/App-Testimonial-5-600x152.jpg"),
                Banner = new Uri("https://images-assets.nasa.gov/image/PIA05062/PIA05062~thumb.jpg"),
                ShortDescription = "Sample project",
                TargetAmount = 1234,
            }
        };

        ProjectsList = new ReadOnlyObservableCollection<IFounderProjectViewModel>(projects);
        LoadProjects = ReactiveCommand.Create(() => Result.Success<IEnumerable<IFounderProjectViewModel>>(ProjectsList)).Enhance();
        Create = ReactiveCommand.Create(() => Result.Success(Maybe<string>.None)).Enhance();
    }

    public IEnhancedCommand<Result<IEnumerable<IFounderProjectViewModel>>> LoadProjects { get; }

    public IReadOnlyCollection<IFounderProjectViewModel> ProjectsList { get; }

    public IEnhancedCommand<Result<Maybe<string>>> Create { get; }
}
