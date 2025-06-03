using System.Threading.Tasks;
using Angor.Contexts.Funding.Founder;
using Angor.Contexts.Funding.Founder.Operations;
using ReactiveUI.SourceGenerators;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.Details;

public partial class InvestmentViewModel : ReactiveObject, IInvestmentViewModel
{
    private readonly Investment2 investment;

    [Reactive]
    private InvestmentStatus status;

    public InvestmentViewModel(Investment2 investment, Func<Task<Maybe<Result<bool>>>> onApprove)
    {
        this.investment = investment;
        var canApprove = this.WhenAnyValue(model => model.Status, investmentStatus => investmentStatus == InvestmentStatus.Pending);
        Approve = ReactiveCommand.CreateFromTask(onApprove, canApprove).Enhance();
        Approve.Values().Successes().Do(_ => Status = InvestmentStatus.Approved).Subscribe();

        Status = investment.Status;
    }

    public IAmountUI Amount => new AmountUI(investment.Amount);
    public string InvestorNostrPubKey => investment.InvestorNostrPubKey;
    public DateTimeOffset CreatedOn => investment.CreatedOn;
    public IEnhancedCommand<Unit, Maybe<Result<bool>>> Approve { get; }
}