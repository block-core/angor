using System.Windows.Input;
using Angor.Contexts.Funding.Founder.Operations;

namespace AngorApp.Sections.Founder;

public interface IFounderSectionViewModel
{
    public ReactiveCommand<Unit, Result<IEnumerable<GetPendingInvestments.PendingInvestmentDto>>> GetPendingInvestments { get; }
    public IEnumerable<GetPendingInvestments.PendingInvestmentDto> Pending { get; }
}