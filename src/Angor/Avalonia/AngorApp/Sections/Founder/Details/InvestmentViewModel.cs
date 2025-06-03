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

    public InvestmentViewModel(GetInvestments.Investment investment, Func<Task<Maybe<Result<bool>>>> onApprove)
    {
        this.investment = investment;
        Approve = ReactiveCommand.CreateFromTask(onApprove).Enhance();
        Approve.Values().Successes().Do(approved => CanApprove = approved).Subscribe();
    }

    public bool CanApprove { get; set; }

    public IAmountUI Amount => new AmountUI(investment.Amount);
    public string InvestorNostrPubKey => investment.InvestorNostrPubKey;
    public DateTimeOffset Created => investment.Created;
    public IEnhancedCommand<Unit, Maybe<Result<bool>>> Approve { get; }
    public InvestmentStatus Status
    {
        get
        {
            if (investment.IsInvested)
            {
                return InvestmentStatus.Invested;
            }

            if (investment.IsApproved)
            {
                return InvestmentStatus.Approved;
            }

            return InvestmentStatus.Pending;
        }
    }
}