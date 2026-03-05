using System;
using AngorApp.Model.ProjectsV2;

namespace AngorApp.UI.Sections.MyProjects;

public interface IMyProjectsSectionViewModel
{
    IReadOnlyCollection<IProject> Projects { get; }
    IEnhancedCommand<Result<IEnumerable<IProject>>> LoadProjects { get; }
    IEnhancedCommand<Result<Maybe<string>>> Create { get; }
    IObservable<int> ActiveProjectsCount { get; }
    IObservable<IAmountUI> TotalRaised { get; }
}
