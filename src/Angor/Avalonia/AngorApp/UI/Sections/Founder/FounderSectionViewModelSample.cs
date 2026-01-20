using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace AngorApp.UI.Sections.Founder;

public class FounderSectionViewModelSample : IFounderSectionViewModel
{
    public FounderSectionViewModelSample()
    {
        ProjectsList = new ObservableCollection<IFounderProjectViewModel>
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

        LoadProjectsCommand = new NoOpCommand();
        CreateCommand = new NoOpCommand();
    }

    public ICommand LoadProjectsCommand { get; }

    public ObservableCollection<IFounderProjectViewModel> ProjectsList { get; }

    public ICommand CreateCommand { get; }

    public bool IsLoading => false;

    public string? ErrorMessage => null;

    private class NoOpCommand : ICommand
    {
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) { }
        public event EventHandler? CanExecuteChanged;
    }
}
