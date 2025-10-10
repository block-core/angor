using System.Reactive.Disposables;
using Angor.Contexts.Funding.Founder;
using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using Angor.Contexts.Funding.Shared;
using Angor.UI.Model.Implementation.Projects;
using AngorApp.Sections.Founder.ProjectDetails.MainView;
using AngorApp.Sections.Founder.ProjectDetails.MainView.Approve;
using AngorApp.UI.Services;
using ReactiveUI.SourceGenerators;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.ProjectDetails;

public partial class FounderProjectDetailsViewModel : ReactiveObject, IFounderProjectDetailsViewModel, IDisposable
{
    private readonly IProjectAppService projectAppService;

    [ObservableAsProperty]
    private IProjectMainViewModel projectMain;
    
    private readonly CompositeDisposable disposable = new();

    public FounderProjectDetailsViewModel(ProjectId projectId, IProjectAppService projectAppService, IFounderAppService founderAppService, UIServices uiServices)
    {
        this.projectAppService = projectAppService;
        Load = ReactiveCommand.CreateFromTask(() => projectAppService.GetFullProject(projectId).Map(IProjectMainViewModel (project) => new ProjectMainViewModel(project, founderAppService, uiServices))).Enhance();
        Load.HandleErrorsWith(uiServices.NotificationService);

        projectMainHelper = Load.Successes().ToProperty(this, x => x.ProjectMain).DisposeWith(disposable);
        Load.Execute().Subscribe().DisposeWith(disposable);
    }

    public IEnhancedCommand<Result<IProjectMainViewModel>> Load { get; }
    
    public void Dispose()
    {
        disposable.Dispose();
    }
}