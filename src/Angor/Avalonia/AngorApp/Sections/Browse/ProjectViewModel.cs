using AngorApp.Model;
using AngorApp.Sections.Browse.Details;
using AngorApp.Sections.Wallet.NoWallet;
using AngorApp.Services;
using CSharpFunctionalExtensions;
using Zafiro.Avalonia.Controls.Navigation;

namespace AngorApp.Sections.Browse;

public class ProjectViewModel : ReactiveObject
{
    private readonly IProject project;

    public ProjectViewModel(IWalletProvider walletProvider, IProject project, INavigator navigator, UIServices uiServices)
    {
        this.project = project;
        GoToDetails = ReactiveCommand.Create(() => navigator.Go(() => new ProjectDetailsViewModel(walletProvider, project, uiServices)));
    }

    public string Name => project.Name;
    public string ShortDescription => project.ShortDescription;
    public Uri Icon => project.Icon;
    public Uri Picture => project.Picture;

    public ReactiveCommand<Unit, Unit> GoToDetails { get; set; }
}