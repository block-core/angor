using System;
using AngorApp.UI.Sections.MyProjects.Items;

namespace AngorApp.UI.Sections.MyProjects;

public interface IMyProjectsSectionViewModel
{
    IReadOnlyCollection<IMyProjectItem> Projects { get; }
    IEnhancedCommand<Result<IEnumerable<IMyProjectItem>>> LoadProjects { get; }
    IEnhancedCommand<Result> RefreshProjectStats { get; }
    IEnhancedCommand<Result<Maybe<string>>> Create { get; }
    IObservable<int> ActiveProjectsCount { get; }
    IObservable<IAmountUI> TotalRaised { get; }
    IObservable<int> ProjectStatsLoadTotalCount { get; }
    IObservable<int> ProjectStatsLoadCompletedCount { get; }
}
