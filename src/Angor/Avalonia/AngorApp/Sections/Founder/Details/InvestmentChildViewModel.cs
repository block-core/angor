using Angor.Contexts.Funding.Founder;
using Angor.Contexts.Funding.Founder.Operations;
using ReactiveUI.SourceGenerators;

namespace AngorApp.Sections.Founder.Details;

public partial class InvestmentChildViewModel(Investment investment) : ReactiveObject, IInvestmentChild
{
    public DateTime CreatedOn => investment.CreatedOn;

    [Reactive]
    private InvestmentStatus status = investment.Status;

    public string InvestorNostrPubKey => investment.InvestorNostrPubKey;

    public IAmountUI Amount => new AmountUI(investment.Amount);
}