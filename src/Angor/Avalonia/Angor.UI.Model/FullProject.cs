using Angor.Contexts.Funding.Projects.Application.Dtos;

namespace Angor.UI.Model;

public class FullProject(ProjectDto info, ProjectStatisticsDto stats)
{
    public ProjectDto Info { get; } = info;
    public ProjectStatisticsDto Stats { get; } = stats;
    public ProjectStatus Status => this.Status();
}
