namespace Angor.Client.Models;

public class ProjectValidationService
{
    public bool CanInvest(Project project)
    {
        if (project == null) return false;

        var now = DateTime.UtcNow;
        if (now < project.ProjectInfo.StartDate) return false; // Investing period hasn't started
        if (now > project.ProjectInfo.ExpiryDate) return false; // Investing period has ended

        return true;
    }
}