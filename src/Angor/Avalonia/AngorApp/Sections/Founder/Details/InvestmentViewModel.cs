using System.Threading.Tasks;
using Angor.Contexts.Funding.Founder.Operations;
using Humanizer;
using ReactiveUI.SourceGenerators;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.Details;

public partial class InvestmentViewModel : ReactiveObject, IInvestmentViewModel
{
    private readonly GetInvestments.Investment investment;

    [Reactive]
    private InvestmentStatus status;

    public InvestmentViewModel(GetInvestments.Investment investment, Func<Task<Maybe<Result<bool>>>> onApprove)
    {
        this.investment = investment;
        var canApprove = this.WhenAnyValue(model => model.Status, investmentStatus => investmentStatus == InvestmentStatus.Pending );
        Approve = ReactiveCommand.CreateFromTask(onApprove, canApprove).Enhance();
        Approve.Values().Successes().Do(_ => Status = InvestmentStatus.Approved).Subscribe();

        Status = GetStatus(investment);
    }

    public IAmountUI Amount => new AmountUI(investment.Amount);
    public string InvestorNostrPubKey => investment.InvestorNostrPubKey;
    public DateTimeOffset Created => investment.Created;
    public IEnhancedCommand<Unit, Maybe<Result<bool>>> Approve { get; }

    private InvestmentStatus GetStatus(GetInvestments.Investment investment1)
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