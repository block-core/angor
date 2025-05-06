using System.Windows.Input;
using Angor.Contexts.Funding.Founder.Operations;
using Angor.Contexts.Funding.Projects.Application.Dtos;

namespace AngorApp.Sections.Founder;

public interface IFounderSectionViewModel
{
    ReactiveCommand<Unit, Result<IEnumerable<GetPendingInvestments.PendingInvestmentDto>>> GetPendingInvestments { get; }
    IEnumerable<GetPendingInvestments.PendingInvestmentDto> Pending { get; }

    IEnumerable<ProjectDto> Projects { get; }

    ReactiveCommand<Unit, Result<IEnumerable<ProjectDto>>> GetProjects { get; }
}