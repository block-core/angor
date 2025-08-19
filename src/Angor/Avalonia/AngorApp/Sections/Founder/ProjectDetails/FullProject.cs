using Angor.Contexts.Funding.Projects.Application.Dtos;
using AngorApp.Sections.Portfolio;

namespace AngorApp.Sections.Founder.ProjectDetails;

public class FullProject(ProjectDto info, ProjectStatisticsDto stats)
{
    
    public ProjectDto Info { get; } = info;
    public ProjectStatisticsDto Stats { get; } = stats;
    public ProjectStatus Status => this.Status();
}
