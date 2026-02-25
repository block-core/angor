using System.Collections.ObjectModel;
using System.Reactive.Linq;
using AngorApp.Model.Funded.Fund.Samples;
using AngorApp.Model.Funded.Investment.Samples;
using AngorApp.UI.Sections.MyProjects.Items;

namespace AngorApp.UI.Sections.MyProjects;

public class MyProjectsSectionViewModelSample : IMyProjectsSectionViewModel
{
    public MyProjectsSectionViewModelSample()
    {
        Projects = new ReadOnlyCollection<IMyProjectItem>(
        [
            new MyProjectItemSample
            {
                Project = new InvestmentProjectSample()
            },
            new MyProjectItemSample
            {
                Project = new FundProjectSample()
            }
        ]);

        ActiveProjectsCount = Observable.Return(3);
        TotalRaised = Observable.Return(new AmountUI(232144));
        LoadProjects = ReactiveCommand.Create(() => Result.Success<IEnumerable<IMyProjectItem>>([])).Enhance();
        RefreshProjectStats = ReactiveCommand.Create(() => Result.Success()).Enhance();
        Create = ReactiveCommand.Create(() => Result.Success(Maybe<string>.None)).Enhance();
        ProjectStatsLoadTotalCount = Observable.Return(3);
        ProjectStatsLoadCompletedCount = Observable.Return(3);
    }

    public IReadOnlyCollection<IMyProjectItem> Projects { get; }
    public IEnhancedCommand<Result<IEnumerable<IMyProjectItem>>> LoadProjects { get; }
    public IEnhancedCommand<Result> RefreshProjectStats { get; }
    public IEnhancedCommand<Result<Maybe<string>>> Create { get; }
    public IObservable<int> ActiveProjectsCount { get; }
    public IObservable<IAmountUI> TotalRaised { get; }
    public IObservable<int> ProjectStatsLoadTotalCount { get; }
    public IObservable<int> ProjectStatsLoadCompletedCount { get; }
}
