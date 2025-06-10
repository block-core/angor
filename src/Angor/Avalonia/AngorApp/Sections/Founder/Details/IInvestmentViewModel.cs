using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.Details;

public interface IInvestmentViewModel
{
    public IAmountUI Amount { get; }
    public string InvestorNostrPubKey { get; }
    public DateTimeOffset CreatedOn { get; }
    public IEnhancedCommand<Unit, Maybe<Result<bool>>> Approve { get; }
    public Angor.Contexts.Funding.Founder.InvestmentStatus Status { get; }
}