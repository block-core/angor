using System.Threading.Tasks;
using Angor.Contexts.Funding.Founder.Operations;
using ReactiveUI.SourceGenerators;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.Details;

public partial class InvestmentViewModel : ReactiveObject, IInvestmentViewModel
{
    [Reactive] private bool isApproved;
    private readonly GetInvestments.Investment investment;

    public InvestmentViewModel(GetInvestments.Investment investment, Func<Task<Maybe<Result>>> onApprove)
    {
        this.investment = investment;
        IsApproved = investment.IsApproved;
        Approve = ReactiveCommand.CreateFromTask(onApprove).Enhance();
        Approve.Values().Successes().Do(_ => IsApproved = true).Subscribe();
    }

    public IAmountUI Amount => new AmountUI(investment.Amount);
    public string InvestorNostrPubKey => investment.InvestorNostrPubKey;
    public DateTimeOffset Created => investment.Created;
    
    public IEnhancedCommand<Unit, Maybe<Result>> Approve { get; }
}