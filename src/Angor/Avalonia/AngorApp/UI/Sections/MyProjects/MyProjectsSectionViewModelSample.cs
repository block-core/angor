using System.Collections.ObjectModel;
using System.Reactive.Linq;
using AngorApp.Model.ProjectsV2;
using AngorApp.Model.Funded.Fund.Samples;
using AngorApp.Model.Funded.Investment.Samples;

namespace AngorApp.UI.Sections.MyProjects;

public class MyProjectsSectionViewModelSample : IMyProjectsSectionViewModel
{
    public MyProjectsSectionViewModelSample()
    {
        Projects = new ReadOnlyCollection<IProject>(
        [
            new InvestmentProjectSample(),
            new FundProjectSample()
        ]);

        ActiveProjectsCount = Observable.Return(3);
        TotalRaised = Observable.Return(new AmountUI(232144));
        LoadProjects = ReactiveCommand.Create(() => Result.Success<IEnumerable<IProject>>([])).Enhance();
        RefreshProjectStats = ReactiveCommand.Create(() => Result.Success()).Enhance();
        Create = ReactiveCommand.Create(() => Result.Success(Maybe<string>.None)).Enhance();
        Observable.Return(3);
        Observable.Return(3);
    }

    public IReadOnlyCollection<IProject> Projects { get; }
    public IEnhancedCommand<Result<IEnumerable<IProject>>> LoadProjects { get; }
    public IEnhancedCommand<Result> RefreshProjectStats { get; }
    public IEnhancedCommand<Result<Maybe<string>>> Create { get; }
    public IObservable<int> ActiveProjectsCount { get; }
    public IObservable<IAmountUI> TotalRaised { get; }
}
