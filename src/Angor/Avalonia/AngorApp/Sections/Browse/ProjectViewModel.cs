using AngorApp.Sections.Browse.Details;
using AngorApp.Sections.Wallet;
using AngorApp.Services;
using CSharpFunctionalExtensions;
using Zafiro.Avalonia.Controls.Navigation;

namespace AngorApp.Sections.Browse;

public class ProjectViewModel : ReactiveObject
{
    private readonly IProject project;

    public ProjectViewModel(Func<Maybe<IWallet>> getWallet, IProject project, INavigator navigator, UIServices uiServices)
    {
        this.project = project;
        GoToDetails = ReactiveCommand.Create(() => navigator.Go(() => new ProjectDetailsViewModel(getWallet, project, uiServices)));
    }

    public string Name => project.Name;
    public string ShortDescription => project.ShortDescription;
    public Uri Icon => project.Icon;
    public Uri Picture => project.Picture;

    public ReactiveCommand<Unit, Unit> GoToDetails { get; set; }
}