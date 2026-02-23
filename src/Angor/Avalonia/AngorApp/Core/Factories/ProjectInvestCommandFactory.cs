using AngorApp.UI.Flows.InvestV2;
using Zafiro.UI.Navigation;

namespace AngorApp.Core.Factories;

public class ProjectInvestCommandFactory : IProjectInvestCommandFactory
{
    private readonly UIServices uiServices;
    private readonly INavigator navigator;

    public ProjectInvestCommandFactory(
        UIServices uiServices,
        INavigator navigator)
    {
        this.uiServices = uiServices;
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
