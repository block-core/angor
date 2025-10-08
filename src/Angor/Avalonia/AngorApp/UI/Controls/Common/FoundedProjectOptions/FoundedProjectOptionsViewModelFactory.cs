using Angor.Contexts.Funding.Investor;
using Angor.Contexts.Funding.Shared;
using AngorApp.UI.Services;
using Zafiro.UI;
using Zafiro.UI.Navigation;

namespace AngorApp.UI.Controls.Common.FoundedProjectOptions;

public class FoundedProjectOptionsViewModelFactory : IFoundedProjectOptionsViewModelFactory
{
    private readonly IInvestmentAppService investmentAppService;
    private readonly UIServices uiServices;
    private readonly INavigator navigator;
    private readonly IWalletContext walletContext;

    public FoundedProjectOptionsViewModelFactory(
        IInvestmentAppService investmentAppService,
        UIServices uiServices,
        INavigator navigator,
        IWalletContext walletContext)
    {
        this.investmentAppService = investmentAppService;
        this.uiServices = uiServices;
        this.navigator = navigator;
        this.walletContext = walletContext;
    }

    public IFoundedProjectOptionsViewModel Create(ProjectId projectId)
    {
        return new FoundedProjectOptionsViewModel(projectId, investmentAppService, uiServices, navigator, walletContext);
    }
}
