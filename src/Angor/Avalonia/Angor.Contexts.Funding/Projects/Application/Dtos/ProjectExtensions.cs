namespace Angor.Contexts.Funding.Projects.Application.Dtos;

// TODO: Check if the logic of these methods is correct
public static class ProjectExtensions
{
    public static long Raised(this ProjectDto project)
    {
        return project.Stages.Sum(stage => stage.Amount);
    }
    
    public static bool HasStarted(this ProjectDto project)
    {
        return DateTime.UtcNow >= project.StartingDate;
    }
    
    public static bool HasReachedTarget(this ProjectDto project)
    {
        return  project.Raised() >= project.TargetAmount;
    }
    
    public static bool IsUnfunded(this ProjectDto project)
    {
        return project.HasStarted() && !project.HasReachedTarget();
    }
}