namespace Angor.UI.Model;

public static class FullProjectExtensions
{
    public static long Raised(this FullProject project)
    {
        return project.Stats.TotalInvested;
    }
    
    public static bool HasStartedFunding(this FullProject project)
    {
        return DateTime.UtcNow >= project.Info.FundingStartDate;
    }
    
    public static bool FundingHasFinished(this FullProject project)
    {
        return DateTime.UtcNow >= project.Info.FundingEndDate;
    }
    
    public static bool HasReachedTarget(this FullProject project)
    {
        return  project.Raised() >= project.Info.TargetAmount;
    }
    
    public static bool IsFailed(this FullProject project)
    {
        return project.FundingHasFinished() && !project.HasReachedTarget();
    }

    public static ProjectStatus Status(this FullProject project)
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