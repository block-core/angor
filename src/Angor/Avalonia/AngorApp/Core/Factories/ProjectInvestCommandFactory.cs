using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Shared;
using Angor.Shared.Models;
using AngorApp.UI.Flows.InvestV2;
using AngorApp.UI.Shared.Services;
using Zafiro.UI.Navigation;

namespace AngorApp.Core.Factories;

public class ProjectInvestCommandFactory : IProjectInvestCommandFactory
{
    private readonly IProjectAppService projectAppService;
    private readonly INotificationService notificationService;
    private readonly Func<IFullProject, IInvestViewModel> investViewModelFactory;
    private readonly INavigator navigator;
    private readonly UIServices uiServices;

    public ProjectInvestCommandFactory(
        IProjectAppService projectAppService,
        INotificationService notificationService,
        Func<IFullProject, IInvestViewModel> investViewModelFactory,
        INavigator navigator,
        UIServices uiServices)
    {
        this.projectAppService = projectAppService;
        this.notificationService = notificationService;
        this.investViewModelFactory = investViewModelFactory;
        this.navigator = navigator;
        this.uiServices = uiServices;
    }

    public IEnhancedCommand<Result> Create(ProjectId projectId, DateTime fundingStart, DateTime fundingEnd, ProjectType projectType)
    {
        var canExecute = Observable.Interval(TimeSpan.FromMinutes(1))
            .StartWith(0L)
            .CombineLatest(
                uiServices.WhenAnyValue(s => s.IsDebugModeEnabled).DistinctUntilChanged(),
                (_, isDebug) => CanInvest(projectType, fundingStart, fundingEnd, isDebug))
            .DistinctUntilChanged();

        var command = EnhancedCommand.CreateWithResult(() =>
        {
            if (!CanInvest(projectType, fundingStart, fundingEnd, uiServices.IsDebugModeEnabled))
            {
                return Task.FromResult(Result.Failure<Unit>("Investment is not available at this time."));
            }

            return projectAppService.GetFullProject(projectId).Bind(project => navigator.Go(() => investViewModelFactory(project)));
        }, canExecute).AsResult();
        command.HandleErrorsWith(notificationService, "Investment failed");
        return command;
    }

    private static bool CanInvest(ProjectType projectType, DateTime fundingStart, DateTime fundingEnd, bool isDebugMode = false)
    {
        return projectType switch
        {
            ProjectType.Invest => CanInvest(projectType, DateTime.UtcNow, fundingStart, fundingEnd, isDebugMode),
            ProjectType.Fund => true,
            ProjectType.Subscribe => true,
            _ => false
        };
    }

    public static bool CanInvest(ProjectType projectType, DateTime currentTime, DateTime fundingStart, DateTime fundingEnd, bool isDebugMode = false)
    {
        return projectType switch
        {
            ProjectType.Invest => IsInsideInvestmentPeriod(currentTime, fundingStart, fundingEnd, isDebugMode),
            ProjectType.Fund => true,
            ProjectType.Subscribe => true,
            _ => false
        };
    }

    public static bool IsInsideInvestmentPeriod(DateTime currentTime, DateTime fundingStart, DateTime fundingEnd, bool isDebugMode = false)
    {
        if (isDebugMode)
        {
            return true;
        }

        var currentDate = currentTime.Date;
        return currentDate >= fundingStart.Date && currentDate <= fundingEnd.Date;
    }
}
