using System.Reactive.Disposables;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Projects.Application.Dtos;
using AngorApp.Sections.Founder.ProjectDetails.Investments;
using AngorApp.Sections.Founder.ProjectDetails.ManageFunds;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;
using Zafiro.UI.Navigation;

namespace AngorApp.Sections.Founder.ProjectDetails;

public partial class FounderProjectDetailsViewModel : ReactiveObject, IFounderProjectDetailsViewModel, IDisposable
{
    private readonly ProjectDto project;
    private readonly CompositeDisposable disposable = new();
    [ObservableAsProperty] private IEnumerable<IInvestmentViewModel> investments;

    public FounderProjectDetailsViewModel(ProjectDto project, IInvestmentAppService investmentAppService, UIServices uiServices, INavigator navigation)
    {
        this.project = project;
        InvestmentsViewModel = new ProjectInvestmentsViewModel(project, investmentAppService, uiServices).DisposeWith(disposable);
        ManageFundsViewModel = new ManageFundsViewModel(project, investmentAppService).DisposeWith(disposable);
        HasProjectStarted = project.HasStarted();
    }

    public Uri? BannerUrl => project.Banner;
    public string ShortDescription => project.ShortDescription;
    public IProjectInvestmentsViewModel InvestmentsViewModel { get; }
    public IManageFundsViewModel ManageFundsViewModel { get; }
    public bool HasProjectStarted { get; }
    public ProjectDto Project => project;
    public string Name => project.Name;

    public void Dispose()
    {
        disposable.Dispose();
    }
}