using System.Windows.Input;
using Angor.Contexts.Wallet.Application;
using AngorApp.Features.Invest;
using AngorApp.Sections.Browse.Details;
using AngorApp.UI.Services;
using Zafiro.Avalonia.Controls.Navigation;
using Zafiro.UI.Navigation;

namespace AngorApp.Sections.Browse;

public class ProjectViewModel(
    IWalletAppService walletAppService,
    IProject project,
    INavigator navigator,
    UIServices uiServices, InvestWizard investWizard)
    : ReactiveObject, IProjectViewModel
{
    public IProject Project { get; } = project;

    public ICommand GoToDetails { get; set; } = ReactiveCommand.Create(() =>
        navigator.Go(() => new ProjectDetailsViewModel(project, investWizard, uiServices)));
}