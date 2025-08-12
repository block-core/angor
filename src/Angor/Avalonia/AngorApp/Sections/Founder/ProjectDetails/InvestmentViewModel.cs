using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Angor.Contexts.Funding.Founder;
using Angor.Contexts.Funding.Founder.Operations;
using Zafiro.UI.Commands;

namespace AngorApp.Sections.Founder.ProjectDetails;

public partial class InvestmentViewModel : ReactiveObject, IInvestmentViewModel, IDisposable
{
    [ReactiveUI.SourceGenerators.Reactive]
    private bool areDetailsShown;

    private readonly CompositeDisposable disposable = new();

    public InvestmentViewModel(IGrouping<InvestmentGroupKey, Investment> group, Func<Task<bool>> onApprove)
    {
        var sorted = group.OrderByDescending(x => x.CreatedOn)
            .Select(investment => new InvestmentChildViewModel(investment))
            .ToList();

        MostRecentInvestment = sorted.First();
        OtherInvestments = sorted.Skip(1);

        Approve = ReactiveCommand.CreateFromTask(async () =>
        {
            var approved = await onApprove();
            if (approved)
            {
                MostRecentInvestment.Status = InvestmentStatus.FounderSignaturesReceived;
            }
            
        }, this.WhenAnyValue(model => model.MostRecentInvestment.Status, x => x == InvestmentStatus.PendingFounderSignatures)).Enhance().DisposeWith(disposable);

        this.WhenAnyValue<InvestmentViewModel, bool>(x => x.AreDetailsShown).Subscribe(b => { });
    }

    public void Dispose()
    {
        disposable.Dispose();
    }

    public IEnumerable<IInvestmentChild> OtherInvestments { get; }

    public IInvestmentChild MostRecentInvestment { get; }

    public IEnhancedCommand Approve { get; }
}