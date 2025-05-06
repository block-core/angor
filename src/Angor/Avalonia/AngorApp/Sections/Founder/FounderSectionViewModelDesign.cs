using Angor.Contexts.Funding.Founder.Operations;

namespace AngorApp.Sections.Founder;

public class FounderSectionViewModelDesign : IFounderSectionViewModel
{
    public ReactiveCommand<Unit, Result<IEnumerable<GetPendingInvestments.PendingInvestmentDto>>> GetPendingInvestments { get; }
    public IEnumerable<GetPendingInvestments.PendingInvestmentDto> Pending { get; }
}