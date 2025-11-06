using AngorApp.Flows.Invest;

namespace AngorApp.Core.Factories;

public class ProjectInvestCommandFactory : IProjectInvestCommandFactory
{
    private readonly InvestFlow investFlow;
    private readonly UIServices uiServices;
    private readonly IWalletContext walletContext;

    public ProjectInvestCommandFactory(
        InvestFlow investFlow,
        UIServices uiServices,
        IWalletContext walletContext)
    {
        this.investFlow = investFlow;
        this.uiServices = uiServices;
        this.walletContext = walletContext;
    }

    public IEnhancedCommand<Result<Maybe<Unit>>> Create(FullProject project, bool isInsideInvestmentPeriod)
    {
        var canExecute = Observable.Return(isInsideInvestmentPeriod);

        var command = ReactiveCommand.CreateFromTask(
                () => walletContext.RequiresWallet(wallet => investFlow.Invest(wallet, project)),
                canExecute)
            .Enhance();

        command.HandleErrorsWith(uiServices.NotificationService, "Investment failed");

        return command;
    }
}
