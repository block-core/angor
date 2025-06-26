using System.Threading.Tasks;
using Angor.Contexts.Funding.Projects.Domain;
using ReactiveUI.SourceGenerators;
using Zafiro.CSharpFunctionalExtensions;
using Zafiro.UI.Commands;
using Investment = Angor.Contexts.Funding.Founder.Operations.Investment;

namespace AngorApp.Sections.Founder.Details;

public partial class InvestmentViewModel : ReactiveObject, IInvestmentViewModel
{
    private readonly Investment investment;

    [Reactive]
    private InvestmentStatus status;

    public InvestmentViewModel(Investment investment, Func<Task<Maybe<Result<bool>>>> onApprove)
    {
        this.investment = investment;
        var canApprove = this.WhenAnyValue(model => model.Status, investmentStatus => investmentStatus == InvestmentStatus.PendingFounderSignatures);
        Approve = ReactiveCommand.CreateFromTask(onApprove, canApprove).Enhance();
        Approve.Values().Successes().Do(_ => Status = InvestmentStatus.FounderSignaturesReceived).Subscribe();

        Status = investment.Status;
    }

    public IAmountUI Amount => new AmountUI(investment.Amount);
    public string InvestorNostrPubKey => investment.InvestorNostrPubKey;
    public DateTimeOffset CreatedOn => investment.CreatedOn;
    public IEnhancedCommand<Unit, Maybe<Result<bool>>> Approve { get; }
}