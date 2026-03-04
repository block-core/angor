using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Shared;
using Angor.Shared.Models;
using AngorApp.UI.Flows.InvestV2;
using Zafiro.UI.Navigation;
using Zafiro.UI.Shell.Utils;

namespace AngorApp.Core.Factories;

public class ProjectInvestCommandFactory : IProjectInvestCommandFactory
{
    private readonly IProjectAppService projectAppService;
    private readonly INotificationService notificationService;
    private readonly Func<IFullProject, IInvestViewModel> investViewModelFactory;
    private readonly INavigator navigator;

    public ProjectInvestCommandFactory(
        IProjectAppService projectAppService,
        INotificationService notificationService,
        Func<IFullProject, IInvestViewModel> investViewModelFactory,
        INavigator navigator)
    {
        this.projectAppService = projectAppService;
        this.notificationService = notificationService;
        this.investViewModelFactory = investViewModelFactory;
        this.navigator = navigator;
    }

    public IEnhancedCommand<Result> Create(ProjectId projectId, DateTimeOffset fundingStart, DateTimeOffset fundingEnd, ProjectType projectType)
    {
        var canExecute = Observable.Interval(TimeSpan.FromMinutes(1))
            .StartWith(0L)
            .Select(_ => CanInvest(projectType, fundingStart, fundingEnd))
            .DistinctUntilChanged();

        var command = EnhancedCommand.CreateWithResult(() =>
        {
            if (!CanInvest(projectType, fundingStart, fundingEnd))
            {
                return Task.FromResult(Result.Failure<Unit>("Investment is not available at this time."));
            }

            return projectAppService.GetFullProject(projectId)
                .Bind(project => navigator.Go(() => investViewModelFactory(project)));
        }, canExecute).AsResult();
        command.HandleErrorsWith(notificationService, "Investment failed");
        return command;
    }

    private static bool CanInvest(ProjectType projectType, DateTimeOffset fundingStart, DateTimeOffset fundingEnd)
    {
        return CanInvest(projectType, DateTimeOffset.UtcNow, fundingStart, fundingEnd);
    }

    public static bool CanInvest(ProjectType projectType, DateTimeOffset currentTime, DateTimeOffset fundingStart, DateTimeOffset fundingEnd)
    {
        return projectType switch
        {
            ProjectType.Invest => IsInsideInvestmentPeriod(currentTime, fundingStart, fundingEnd),
            ProjectType.Fund => currentTime >= fundingStart,
            ProjectType.Subscribe => currentTime >= fundingStart,
            _ => false
        };
    }

    public static bool IsInsideInvestmentPeriod(DateTimeOffset currentTime, DateTimeOffset fundingStart, DateTimeOffset fundingEnd)
    {
        return currentTime >= fundingStart && currentTime <= fundingEnd;
    }
}
