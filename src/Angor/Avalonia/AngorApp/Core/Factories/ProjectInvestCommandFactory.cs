using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Shared;
using Angor.Shared.Models;
using AngorApp.UI.Flows.InvestV2;
using AngorApp.UI.Shared.Services;
using Zafiro.UI.Navigation;
using Zafiro.UI.Shell.Utils;

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

    public IEnhancedCommand<Result> Create(ProjectId projectId, DateTimeOffset fundingStart, DateTimeOffset fundingEnd, ProjectType projectType)
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

            return projectAppService.GetFullProject(projectId)
                .Bind(project => navigator.Go(() => investViewModelFactory(project)));
        }, canExecute).AsResult();
        command.HandleErrorsWith(notificationService, "Investment failed");
        return command;
    }

    private static bool CanInvest(ProjectType projectType, DateTimeOffset fundingStart, DateTimeOffset fundingEnd, bool isDebugMode = false)
    {
        return CanInvest(projectType, DateTimeOffset.UtcNow, fundingStart, fundingEnd, isDebugMode);
    }

    public static bool CanInvest(ProjectType projectType, DateTimeOffset currentTime, DateTimeOffset fundingStart, DateTimeOffset fundingEnd, bool isDebugMode = false)
    {
        return projectType switch
        {
            ProjectType.Invest => IsInsideInvestmentPeriod(currentTime, fundingStart, fundingEnd, isDebugMode),
            ProjectType.Fund => currentTime >= fundingStart,
            ProjectType.Subscribe => currentTime >= fundingStart,
            _ => false
        };
    }

    public static bool IsInsideInvestmentPeriod(DateTimeOffset currentTime, DateTimeOffset fundingStart, DateTimeOffset fundingEnd, bool isDebugMode = false)
    {
        if (isDebugMode)
        {
            return true;
        }

        return currentTime >= fundingStart && currentTime <= fundingEnd;
    }
}
