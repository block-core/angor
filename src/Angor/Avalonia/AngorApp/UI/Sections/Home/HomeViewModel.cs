using System.Reactive.Disposables;
using AngorApp.UI.Shell;
using Zafiro.UI.Shell.Utils;

namespace AngorApp.UI.Sections.Home;

[Section("Home", icon: "fa-home")]
public class HomeSectionSectionViewModel : IHomeSectionViewModel, IDisposable
{
    private readonly CompositeDisposable disposable = new();

    public HomeSectionSectionViewModel(IShellViewModel shellViewModel, ICreateProjectFlow createProjectFlow, UIServices uiServices)
    {
        FindProjects = ReactiveCommand.Create(() => shellViewModel.SetSection("Find Projects")).Enhance().DisposeWith(disposable);
        var createProject = ReactiveCommand.CreateFromTask(createProjectFlow.CreateProject).Enhance().DisposeWith(disposable);
        createProject.HandleErrorsWith(uiServices.NotificationService, "Cannot create project").DisposeWith(disposable);
        
        CreateProject = createProject;
    }

    public IEnhancedCommand FindProjects { get; set; }
    public IEnhancedCommand CreateProject { get; set; }

    public void Dispose()
    {
        disposable.Dispose();
    }
}