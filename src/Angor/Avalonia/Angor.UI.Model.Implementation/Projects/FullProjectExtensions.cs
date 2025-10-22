using Angor.Contexts.Funding.Projects.Infrastructure.Interfaces;
using Angor.Contexts.Funding.Shared;
using CSharpFunctionalExtensions;

namespace Angor.UI.Model.Implementation.Projects;

public static class FullProjectExtensions
{
    public static Task<Result<IFullProject>> GetFullProject(this IProjectAppService projectAppService, ProjectId projectId)
    {
        return from project in projectAppService.Get(projectId)
            from stats in projectAppService.GetProjectStatistics(projectId)
            select (IFullProject) new FullProject(project, stats);
    }
    
    public static IAmountUI Raised(this IFullProject project)
    {
        return project.TotalInvested;
    }
    
    public static bool HasStartedFunding(this IFullProject project)
    {
        return DateTime.UtcNow.Date >= project.FundingStartDate.Date;
    }
    
    public static bool FundingHasFinished(this IFullProject project)
    {
        return DateTime.UtcNow.Date >= project.FundingEndDate.Date;
    }
    
    public static bool HasReachedTarget(this IFullProject project)
    {
        return  project.TotalInvested.Sats >= project.TargetAmount.Sats;
    }
    
    public static bool IsFailed(this IFullProject project)
    {
        return project.FundingHasFinished() && !project.HasReachedTarget();
    }

    public static ProjectStatus Status(this IFullProject project)
    {
        if (project.HasStartedFunding())
        {
            if (project.FundingHasFinished())
            {
                return project.HasReachedTarget() ? ProjectStatus.Succeeded : ProjectStatus.Failed;
            }
            return ProjectStatus.Funding;
        }

        return ProjectStatus.Started;
    }
}