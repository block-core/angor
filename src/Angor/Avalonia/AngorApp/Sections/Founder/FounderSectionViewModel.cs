using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using ProjectId = Angor.Contexts.Funding.Shared.ProjectId;
using Angor.UI.Model.Flows;
using Angor.UI.Model.Implementation.Common;
using ReactiveUI;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.Reactive;
using Zafiro.UI;

namespace AngorApp.Sections.Founder;

public class FounderSectionViewModel : ReactiveObject, IFounderSectionViewModel, IDisposable
{
    private readonly CompositeDisposable disposable = new();
    private readonly RefreshableCollection<IFounderProjectViewModel, ProjectId> projectsCollection;

    public FounderSectionViewModel(
        UIServices uiServices,
        IProjectAppService projectAppService,
        ICreateProjectFlow createProjectFlow,
        IWalletContext walletContext,
        IFounderProjectViewModelFactory projectViewModelFactory)
    {
        projectsCollection = RefreshableCollection.Create(
                () => walletContext.RequiresWallet(wallet => projectAppService
                    .GetFounderProjects(wallet.Id.Value)
                    .MapEach(projectViewModelFactory.Create)),
                project => project.Id)
            .DisposeWith(disposable);

        LoadProjects = projectsCollection.Refresh;
        LoadProjects.HandleErrorsWith(uiServices.NotificationService, "Failed to get investments").DisposeWith(disposable);

        ProjectsList = projectsCollection.Items;

        Create = ReactiveCommand.CreateFromTask(() => createProjectFlow.CreateProject()).Enhance().DisposeWith(disposable);
        Create.HandleErrorsWith(uiServices.NotificationService, "Cannot create project").DisposeWith(disposable);
    }

    public void Dispose()
    {
        disposable.Dispose();
    }

    public IReadOnlyCollection<IFounderProjectViewModel> ProjectsList { get; }

    public IEnhancedCommand<Result<IEnumerable<IFounderProjectViewModel>>> LoadProjects { get; }

    public IEnhancedCommand<Result<Maybe<string>>> Create { get; }
}
