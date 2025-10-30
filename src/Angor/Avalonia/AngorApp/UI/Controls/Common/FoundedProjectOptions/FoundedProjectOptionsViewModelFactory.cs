using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Shared;
using AngorApp.Core;
using AngorApp.UI.Services;
using Zafiro.UI;
using Zafiro.UI.Navigation;

namespace AngorApp.UI.Controls.Common.FoundedProjectOptions;

public class FoundedProjectOptionsViewModelFactory(
    IInvestmentAppService investmentAppService,
    UIServices uiServices,
    INavigator navigator,
    IWalletContext walletContext,
    SharedCommands sharedCommands)
    : IFoundedProjectOptionsViewModelFactory
{
    public IFoundedProjectOptionsViewModel Create(ProjectId projectId)
    {
        return new FoundedProjectOptionsViewModel(projectId, investmentAppService, uiServices, navigator, walletContext, sharedCommands);
    }
}
