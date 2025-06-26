using Angor.Contexts.Funding.Projects.Domain;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.Details;

public interface IInvestmentViewModel
{
    public IAmountUI Amount { get; }
    public string InvestorNostrPubKey { get; }
    public DateTimeOffset CreatedOn { get; }
    public IEnhancedCommand<Unit, Maybe<Result<bool>>> Approve { get; }
    public InvestmentStatus Status { get; }
}