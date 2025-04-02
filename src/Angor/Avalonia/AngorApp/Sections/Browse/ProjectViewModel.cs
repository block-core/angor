using System.Windows.Input;
using Angor.Contexts.Wallet.Application;
using AngorApp.Sections.Browse.Details;
using AngorApp.UI.Services;
using Zafiro.Avalonia.Controls.Navigation;

namespace AngorApp.Sections.Browse;

public class ProjectViewModel(
    IWalletAppService walletAppService,
    IProject project,
    INavigator navigator,
    UIServices uiServices)
    : ReactiveObject, IProjectViewModel
{
    public IProject Project { get; } = project;

    public ICommand GoToDetails { get; set; } = ReactiveCommand.Create(() =>
        navigator.Go(() => new ProjectDetailsViewModel(walletAppService, project, uiServices)));
}