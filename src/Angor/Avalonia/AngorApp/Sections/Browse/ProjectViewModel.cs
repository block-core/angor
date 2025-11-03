using System;
using AngorApp.Model.Projects;

namespace AngorApp.Sections.Browse;

public class ProjectViewModel : ReactiveObject, IProjectViewModel, IDisposable
{
    public ProjectViewModel(IProject project, IEnhancedCommand<Result> goToDetails)
    {
        Project = project;
        GoToDetails = goToDetails;
    }

    public IProject Project { get; }

    public IEnhancedCommand<Result> GoToDetails { get; }

    public void Dispose()
    {
        GoToDetails.Dispose();
    }
}
