using AngorApp.UI.Flows.Invest;
using AngorApp.UI.Flows.InvestV2;
using Zafiro.UI.Navigation;

namespace AngorApp.Core.Factories;

public class ProjectInvestCommandFactory : IProjectInvestCommandFactory
{
    private readonly InvestFlow investFlow;
    private readonly UIServices uiServices;
    private readonly IWalletContext walletContext;
    private readonly INavigator navigator;

    public ProjectInvestCommandFactory(
        InvestFlow investFlow,
        UIServices uiServices,
        IWalletContext walletContext, INavigator navigator)
    {
        this.investFlow = investFlow;
        this.uiServices = uiServices;
        this.walletContext = walletContext;
        this.navigator = navigator;
    }

    public IEnhancedCommand<Result<Unit>> Create(FullProject project, bool isInsideInvestmentPeriod)
    {
        var canExecute = Observable.Return(isInsideInvestmentPeriod);
        var command = EnhancedCommand.CreateWithResult(() => navigator.Go<IInvestViewModel>(), canExecute);
        command.HandleErrorsWith(uiServices.NotificationService, "Investment failed");
        return command;
    }
}
