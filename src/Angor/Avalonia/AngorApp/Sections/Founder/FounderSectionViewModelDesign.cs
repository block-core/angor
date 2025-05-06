using Angor.Contexts.Funding.Founder.Operations;
using Angor.Contexts.Funding.Projects.Application.Dtos;

namespace AngorApp.Sections.Founder;

public class FounderSectionViewModelDesign : IFounderSectionViewModel
{
    public ReactiveCommand<Unit, Result<IEnumerable<GetPendingInvestments.PendingInvestmentDto>>> GetPendingInvestments { get; }
    public IEnumerable<GetPendingInvestments.PendingInvestmentDto> Pending { get; }
    public IEnumerable<ProjectDto> Projects { get; }
    public ReactiveCommand<Unit, Result<IEnumerable<ProjectDto>>> GetProjects { get; set; }
}